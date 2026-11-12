using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/cards/batch")]
public class BatchCardsController(
    AppDbContext db,
    AccessService access,
    IDataProtectionProvider protection,
    TimeProvider clock) : ControllerBase
{
    public record Input(Guid RequestId, string Kind, Guid[] Ids, bool ConfirmReissue, Guid? YearId = null);
    [HttpPost]
    public async Task<object> Create(Input input, CancellationToken ct)
    {
        var actor = Guid.Parse(User.FindFirst("sub")!.Value);
        await access.Require(actor, Permission.CredentialManage, ct);
        if (input.RequestId == Guid.Empty
            || !input.ConfirmReissue
            || input.Kind is not ("students" or "catechists")
            || input.Ids == null
            || input.Ids.Length is < 1 or > 100
            || input.Ids.Distinct().Count() != input.Ids.Length)
            throw new AppException(400, "BATCH_INVALID", "Chọn 1–100 người và xác nhận cấp lại.");
        var payload = AuthService.Hash(JsonSerializer.Serialize(new
        {
            input.Kind,
            input.YearId,
            Ids = input.Ids.OrderBy(x => x)
        }));
        var protector = protection.CreateProtector("CheckInApp.BatchCards.v1");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (db.Database.IsSqlServer())
            await db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actor}").LoadAsync(ct);
        var previous = await db.Audit.FirstOrDefaultAsync(x => x.ActorId == actor && x.EntityId == input.RequestId && x.Action == "BatchCards", ct);
        if (previous != null)
        {
            var cache = JsonSerializer.Deserialize<Cache>(previous.Detail)!;
            if (cache.Payload != payload)
                throw new AppException(409, "IDEMPOTENCY_CONFLICT", "Mã yêu cầu đã dùng cho lô khác.");
            if (previous.CreatedAtUtc.AddMinutes(15) < clock.GetUtcNow().UtcDateTime)
                throw new AppException(409, "BATCH_EXPIRED", "Phiên in hết hạn. Không tự cấp lại; hãy tạo lô mới có xác nhận.");
            var existing = input.Kind == "students"
                ? await db.Students.CountAsync(x => input.Ids.Contains(x.Id), ct)
                : await db.Catechists.CountAsync(x => input.Ids.Contains(x.Id), ct);
            if (existing != input.Ids.Length)
                throw new AppException(409, "BATCH_DELETED", "Hồ sơ trong lô đã bị xóa.");
            return JsonSerializer.Deserialize<JsonElement>(protector.Unprotect(cache.Encrypted));
        }
        var ids = await access.ClassIds(actor, ct);
        var rights = await access.Rights(actor, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = new List<object>();
        foreach (var id in input.Ids)
        {
            var membership = input.Kind == "students"
                ? db.Enrollments.Where(x => x.StudentId == id).Select(x => x.ClassRoomId)
                : db.Assignments.Where(x => x.CatechistId == id).Select(x => x.ClassRoomId);
            var labels = await (from c in db.Classes
                                join y in db.Years on c.AcademicYearId equals y.Id
                                where membership.Contains(c.Id)
                                && (rights.Global || ids.Contains(c.Id))
                                && (input.YearId == null || y.Id == input.YearId)
                                select new
                                {
                                    ClassName = c.Name,
                                    YearName = y.Name
                                }).ToListAsync(ct);
            var className = string.Join(", ", labels.Select(x => x.ClassName).Distinct());
            var academicYearName = string.Join(", ", labels.Select(x => x.YearName).Distinct());
            if (input.Kind == "students")
            {
                var p = await db.Students.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (p == null
                    || !rights.Global && !await db.Enrollments.AnyAsync(x => x.StudentId == id && ids.Contains(x.ClassRoomId), ct))
                    throw new AppException(403, "CLASS_SCOPE", "Học sinh ngoài phạm vi.");
                foreach (var old in await db.Credentials.Where(x => x.StudentId == id && x.RevokedAtUtc == null).ToListAsync(ct))
                    old.RevokedAtUtc = now;
                var token = "CI1-" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
                db.Credentials.Add(new()
                {
                    StudentId = id,
                    TokenHash = AuthService.Hash(token),
                    IssuedAtUtc = now
                });
                rows.Add(new
                {
                    className,
                    academicYearName,
                    id = p.Id,
                    fullName = p.FullName,
                    saintName = p.SaintName,
                    code = p.Code,
                    dateOfBirth = p.DateOfBirth,
                    credential = token
                });
            }
            else
            {
                var p = await db.Catechists.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (p == null
                    || !rights.Global && !await db.Assignments.AnyAsync(x => x.CatechistId == id && ids.Contains(x.ClassRoomId), ct))
                    throw new AppException(403, "CLASS_SCOPE", "GLV ngoài phạm vi.");
                foreach (var old in await db.CatechistCredentials.Where(x => x.CatechistId == id && x.RevokedAtUtc == null).ToListAsync(ct))
                    old.RevokedAtUtc = now;
                var token = "CG1-" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
                db.CatechistCredentials.Add(new()
                {
                    CatechistId = id,
                    TokenHash = AuthService.Hash(token),
                    IssuedAtUtc = now
                });
                rows.Add(new
                {
                    id = p.Id,
                    fullName = p.FullName,
                    saintName = p.SaintName,
                    code = "GLV-" + id.ToString("N")[..8],
                    credential = token
                });
            }
        }
        var result = JsonSerializer.Serialize(new
        {
            items = rows,
            requestId = input.RequestId
        });
        db.Audit.Add(new()
        {
            ActorId = actor,
            EntityId = input.RequestId,
            EntityType = input.Kind,
            Action = "BatchCards",
            Detail = JsonSerializer.Serialize(new Cache(payload, protector.Protect(result)))
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return JsonSerializer.Deserialize<JsonElement>(result);
    }
    record Cache(string Payload, string Encrypted);
}
