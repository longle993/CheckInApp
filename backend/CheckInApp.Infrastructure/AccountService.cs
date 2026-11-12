using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheckInApp.Infrastructure;

public class AccountService(AppDbContext db, AccessService access) : IAccountService
{
    async Task<AppUser> Target(Guid actorId, Guid userId, CancellationToken ct)
    {
        await access.Require(actorId, Permission.UserManage, ct);
        var caller = await access.Rights(actorId, ct);
        var roles = await (from userRole in db.UserRoles
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where userRole.UserId == userId
                           select role).ToListAsync(ct);
        if (roles.Any(role => (role.Permissions & caller.Mask) != role.Permissions || role.GlobalScope && !caller.Global))
            throw new AppException(403, "GRANT_SCOPE", "Không quản lý tài khoản có quyền cao hơn.");
        return await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException(404, "NOT_FOUND", "Không có tài khoản.");
    }

    public async Task Edit(Guid actorId, Guid userId, EditUserInput input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var user = await Target(actorId, userId, ct);
        var profileVersion = await db.Audit
            .Where(a => a.EntityId == userId && a.Action == "EditProfile")
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.Id)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(ct);
        if (profileVersion != input.ProfileVersion || user.FullName != input.ExpectedFullName || user.Email != input.ExpectedEmail)
            throw new AppException(409, "VERSION_CONFLICT", "Thông tin đã thay đổi, tải lại trước khi sửa.");
        var name = input.FullName?.Trim();
        var email = input.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name)
            || name.Length > 200
            || !System.Net.Mail.MailAddress.TryCreate(email, out _)
            || string.IsNullOrWhiteSpace(input.Reason)
            || input.Reason.Length > 500)
            throw new AppException(400, "VALIDATION", "Kiểm tra họ tên, email và lý do.");
        if (await db.Users.AnyAsync(x => x.Id != userId && x.Email == email, ct))
            throw new AppException(409, "EMAIL_EXISTS", "Email đã được sử dụng.");
        if (input.RoleId != null)
        {
            await access.Require(actorId, Permission.RoleManage, ct);
            var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == input.RoleId, ct)
                ?? throw new AppException(400, "ROLE_INVALID", "Vai trò không hợp lệ.");
            var rights = await access.Rights(actorId, ct);
            if ((role.Permissions & rights.Mask) != role.Permissions || role.GlobalScope && !rights.Global)
                throw new AppException(403, "GRANT_SCOPE", "Không cấp quyền vượt quyền hiện tại.");
            var adminIds = await (from userRole in db.UserRoles
                                  join currentRole in db.Roles on userRole.RoleId equals currentRole.Id
                                  where currentRole.Name == "Admin"
                                  select userRole.UserId).ToListAsync(ct);
            if (adminIds.Contains(userId)
                && role.Name != "Admin"
                && await db.Users.CountAsync(x => adminIds.Contains(x.Id) && x.Status == AccountStatus.Active, ct) <= 1)
                throw new AppException(409, "LAST_ADMIN", "Không hạ quyền admin cuối cùng.");
            if (!role.GlobalScope && input.CatechistId == null)
                throw new AppException(400, "CATECHIST_REQUIRED", "Vai trò theo lớp cần GLV liên kết.");
            db.UserRoles.RemoveRange(await db.UserRoles.Where(x => x.UserId == userId).ToListAsync(ct));
            db.UserRoles.Add(new()
            {
                UserId = userId,
                RoleId = role.Id
            });
        }
        if (input.UpdateCatechistLink || input.RoleId != null)
        {
            await access.Require(actorId, Permission.RoleManage, ct);
            if (input.CatechistId != null)
            {
                var teacher = await db.Catechists.SingleOrDefaultAsync(x => x.Id == input.CatechistId, ct)
                    ?? throw new AppException(400, "CATECHIST_INVALID", "GLV không hợp lệ.");
                if (teacher.UserId != null && teacher.UserId != userId)
                    throw new AppException(409, "CATECHIST_LINKED", "GLV đã liên kết tài khoản khác.");
                foreach (var old in await db.Catechists.Where(x => x.UserId == userId && x.Id != input.CatechistId).ToListAsync(ct))
                    old.UserId = null;
                teacher.UserId = userId;
            }
            else
            {
                var currentRoles = await (from userRole in db.UserRoles
                                          join role in db.Roles on userRole.RoleId equals role.Id
                                          where userRole.UserId == userId
                                          select role).ToListAsync(ct);
                if (currentRoles.Any(role => !role.GlobalScope))
                    throw new AppException(400, "CATECHIST_REQUIRED", "Vai trò theo lớp cần GLV liên kết.");
                foreach (var old in await db.Catechists.Where(x => x.UserId == userId).ToListAsync(ct))
                    old.UserId = null;
            }
        }
        var before = new
        {
            user.FullName,
            user.Email
        };
        user.FullName = name;
        user.Email = email!;
        foreach (var session in await db.AuthSessions.Where(x => x.UserId == userId && !x.Revoked).ToListAsync(ct))
        {
            session.Revoked = true;
            session.Version = Guid.NewGuid();
        }
        db.Audit.Add(new()
        {
            ActorId = actorId,
            EntityId = userId,
            EntityType = "User",
            Action = "EditProfile",
            Detail = JsonSerializer.Serialize(new
            {
                before,
                after = new
                {
                    user.FullName,
                    user.Email
                },
                input.Reason,
                input.RoleId,
                input.CatechistId
            })
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task Reconsider(Guid actorId, Guid userId, string? reason, CancellationToken ct)
    {
        await access.Require(actorId, Permission.UserApprove, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var user = await Target(actorId, userId, ct);
        if (user.Status is not (AccountStatus.Suspended or AccountStatus.Rejected))
            throw new AppException(409, "ACCOUNT_STATE", "Chỉ xem xét lại tài khoản khóa hoặc từ chối.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new AppException(400, "REASON_REQUIRED", "Cần lý do xem xét lại.");
        user.Status = AccountStatus.PendingApproval;
        db.Audit.Add(new()
        {
            ActorId = actorId,
            EntityId = userId,
            EntityType = "User",
            Action = "Reconsider",
            Detail = reason
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
