using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public class AdminService(AppDbContext db, AccessService access, TimeProvider clock) : IAdminService
{
    public async Task<object> CatechistCard(Guid actor, Guid id, CancellationToken ct)
    {
        await access.Require(actor, Permission.CredentialManage, ct);
        var rights = await access.Rights(actor, ct);
        var classes = await access.ClassIds(actor, ct);
        var today = AttendanceRules.Today(clock);
        if (!rights.Global
            && !await db.Assignments.AnyAsync(
            x => x.CatechistId == id
            && classes.Contains(x.ClassRoomId)
            && x.From <= today
            && (x.Until == null || today < x.Until),
            ct))
            throw new AppException(403, "CLASS_SCOPE", "Giáo lý viên ngoài phạm vi phụ trách.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var query = db.Database.IsSqlServer()
            ? db.Catechists.FromSqlInterpolated($"SELECT * FROM [Catechists] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
            : db.Catechists.Where(x => x.Id == id);
        if (!await query.AnyAsync(ct))
            throw new AppException(404, "NOT_FOUND", "Không có giáo lý viên.");
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var old in await db.CatechistCredentials.Where(x => x.CatechistId == id && x.RevokedAtUtc == null).ToListAsync(ct))
            old.RevokedAtUtc = now;
        var token = "CG1-" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        db.CatechistCredentials.Add(new()
        {
            CatechistId = id,
            TokenHash = AuthService.Hash(token),
            IssuedAtUtc = now
        });
        db.Audit.Add(new()
        {
            ActorId = actor,
            Action = "IssueCredential",
            EntityType = "Catechist",
            EntityId = id
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var assigned = await (from a in db.Assignments
                              join c in db.Classes on a.ClassRoomId equals c.Id
                              join y in db.Years on c.AcademicYearId equals y.Id
                              where a.CatechistId == id
                                  && a.From <= today
                                  && (a.Until == null || today < a.Until)
                                  && (rights.Global || classes.Contains(c.Id))
                              orderby c.Name
                              select new
                              {
                                  ClassName = c.Name,
                                  YearName = y.Name
                              }).ToListAsync(ct);
        return new
        {
            credential = token,
            catechistId = id,
            personType = "Catechist",
            code = "GLV-" + id.ToString("N")[..8].ToUpperInvariant(),
            className = string.Join(", ", assigned.Select(x => x.ClassName).Distinct()),
            academicYearName = string.Join(", ", assigned.Select(x => x.YearName).Distinct())
        };
    }
    public async Task Approve(Guid actor, Guid userId, Guid roleId, Guid? catechistId, CancellationToken ct)
    {
        await access.Require(actor, Permission.UserApprove | Permission.RoleManage, ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw new AppException(404, "NOT_FOUND", "Không có tài khoản.");
        if (user.Status != AccountStatus.PendingApproval)
            throw new AppException(409, "ACCOUNT_STATE", "Chỉ duyệt tài khoản đang chờ.");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId, ct) ?? throw new AppException(400, "ROLE_INVALID", "Vai trò không hợp lệ.");
        var rights = await access.Rights(actor, ct);
        if ((role.Permissions & rights.Mask) != role.Permissions || (role.GlobalScope && !rights.Global))
            throw new AppException(403, "GRANT_SCOPE", "Không thể cấp vai trò này.");
        if (!role.GlobalScope && catechistId == null)
            throw new AppException(400, "CATECHIST_REQUIRED", "Cần gắn hồ sơ giáo lý viên cho vai trò theo lớp.");
        if (catechistId != null)
        {
            var ca = await db.Catechists.SingleOrDefaultAsync(x => x.Id == catechistId, ct) ?? throw new AppException(400, "CATECHIST_INVALID", "Không có giáo lý viên.");
            if (ca.UserId != null && ca.UserId != user.Id)
                throw new AppException(409, "CATECHIST_LINKED", "Hồ sơ đã liên kết tài khoản.");
            ca.UserId = user.Id;
        }
        user.Status = AccountStatus.Active;
        user.ApprovedBy = actor;
        user.ApprovedAtUtc = clock.GetUtcNow().UtcDateTime;
        db.UserRoles.RemoveRange(await db.UserRoles.Where(x => x.UserId == user.Id).ToListAsync(ct));
        db.UserRoles.Add(new()
        {
            UserId = user.Id,
            RoleId = role.Id
        });
        db.Audit.Add(new()
        {
            ActorId = actor,
            Action = "Approve",
            EntityType = "User",
            EntityId = user.Id,
            Detail = role.Name
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task Status(Guid actor, Guid id, bool reject, CancellationToken ct)
    {
        await access.Require(actor, Permission.UserApprove, ct);
        if (actor == id)
            throw new AppException(400, "SELF_LOCK", "Không tự khóa tài khoản đang dùng.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "NOT_FOUND", "Không có tài khoản.");
        if (reject && user.Status != AccountStatus.PendingApproval)
            throw new AppException(409, "ACCOUNT_STATE", "Chỉ từ chối tài khoản chờ duyệt.");
        var targetRoles = await (from ur in db.UserRoles
                                 join r in db.Roles on ur.RoleId equals r.Id
                                 where ur.UserId == id
                                 select r).ToListAsync(ct);
        var caller = await access.Rights(actor, ct);
        if (targetRoles.Any(r => (r.Permissions & caller.Mask) != r.Permissions || (r.GlobalScope && !caller.Global)))
            throw new AppException(403, "GRANT_SCOPE", "Không quản lý tài khoản có quyền cao hơn.");
        var adminIds = await (from ur in db.UserRoles
                              join r in db.Roles on ur.RoleId equals r.Id
                              where r.Name == "Admin"
                              select ur.UserId).ToListAsync(ct);
        if (adminIds.Contains(id) && await db.Users.CountAsync(x => adminIds.Contains(x.Id) && x.Status == AccountStatus.Active, ct) <= 1)
            throw new AppException(409, "LAST_ADMIN", "Không khóa admin cuối cùng.");
        user.Status = reject ? AccountStatus.Rejected : AccountStatus.Suspended;
        foreach (var s in await db.AuthSessions.Where(x => x.UserId == id && !x.Revoked).ToListAsync(ct))
        {
            s.Revoked = true;
            s.Version = Guid.NewGuid();
        }
        db.Audit.Add(new()
        {
            ActorId = actor,
            Action = reject ? "Reject" : "Suspend",
            EntityType = "User",
            EntityId = id
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task<object> Credential(Guid actor, Guid studentId, CancellationToken ct)
    {
        await access.Require(actor, Permission.CredentialManage, ct);
        var classes = await access.ClassIds(actor, ct);
        var today = AttendanceRules.Today(clock);
        if (!await db.Enrollments.AnyAsync(
            x => x.StudentId == studentId
            && classes.Contains(x.ClassRoomId)
            && x.From <= today
            && (x.Until == null || today < x.Until),
            ct))
            throw new AppException(403, "CLASS_SCOPE", "Không truy cập được học sinh.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var old in await db.Credentials.Where(x => x.StudentId == studentId && x.RevokedAtUtc == null).ToListAsync(ct))
            old.RevokedAtUtc = now;
        var token = "CI1-" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        db.Credentials.Add(new()
        {
            StudentId = studentId,
            TokenHash = AuthService.Hash(token),
            IssuedAtUtc = now
        });
        db.Audit.Add(new()
        {
            ActorId = actor,
            Action = "IssueCredential",
            EntityType = "Student",
            EntityId = studentId
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new
        {
            credential = token,
            studentId,
            note = "Chỉ hiển thị một lần. Thẻ cũ đã thu hồi."
        };
    }
    public async Task Correct(Guid actor, Guid recordId, bool present, Guid version, string reason, CancellationToken ct)
    {
        await access.Require(actor, Permission.AttendanceCorrect, ct);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new AppException(400, "REASON_REQUIRED", "Cần lý do điều chỉnh (tối đa 500 ký tự).");
        var record = await db.Attendance.SingleOrDefaultAsync(x => x.Id == recordId, ct) ?? throw new AppException(404, "NOT_FOUND", "Chưa có bản ghi điểm danh.");
        var session = await db.Sessions.SingleAsync(x => x.Id == record.SessionId, ct);
        await access.Class(actor, session.ClassRoomId, ct);
        if (record.Version != version)
            throw new AppException(409, "VERSION_CONFLICT", "Kết quả đã thay đổi. Hãy tải lại.");
        db.Audit.Add(new()
        {
            ActorId = actor,
            Action = "AttendanceCorrection",
            EntityType = "Attendance",
            EntityId = record.Id,
            Detail = $"{record.Present} -> {present}. {reason.Trim()}"
        });
        record.Present = present;
        record.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
    }
}
