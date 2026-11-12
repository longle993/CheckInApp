using System.Security.Cryptography;
using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CheckInApp.Infrastructure;

public class BatchCardsService(
    AppDbContext db,
    AccessService access,
    IDataProtectionProvider protection,
    TimeProvider clock) : IBatchCardsService
{
    record Cache(string Payload, string Encrypted);

    public async Task<object> Create(Guid actorId, BatchCardsInput input, CancellationToken ct)
    {
        await access.Require(actorId, Permission.CredentialManage, ct);
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
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (db.Database.IsSqlServer())
            await db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actorId}").LoadAsync(ct);
        var previous = await db.Audit.FirstOrDefaultAsync(
            x => x.ActorId == actorId && x.EntityId == input.RequestId && x.Action == "BatchCards",
            ct);
        if (previous != null)
        {
            var cached = JsonSerializer.Deserialize<Cache>(previous.Detail)!;
            if (cached.Payload != payload)
                throw new AppException(409, "IDEMPOTENCY_CONFLICT", "Mã yêu cầu đã dùng cho lô khác.");
            if (previous.CreatedAtUtc.AddMinutes(15) < clock.GetUtcNow().UtcDateTime)
                throw new AppException(409, "BATCH_EXPIRED", "Phiên in hết hạn. Không tự cấp lại; hãy tạo lô mới có xác nhận.");
            var existing = input.Kind == "students"
                ? await db.Students.CountAsync(x => input.Ids.Contains(x.Id), ct)
                : await db.Catechists.CountAsync(x => input.Ids.Contains(x.Id), ct);
            if (existing != input.Ids.Length)
                throw new AppException(409, "BATCH_DELETED", "Hồ sơ trong lô đã bị xóa.");
            return JsonSerializer.Deserialize<JsonElement>(protector.Unprotect(cached.Encrypted));
        }
        var classIds = await access.ClassIds(actorId, ct);
        var rights = await access.Rights(actorId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = new List<object>();
        foreach (var id in input.Ids)
        {
            var membership = input.Kind == "students"
                ? db.Enrollments.Where(x => x.StudentId == id).Select(x => x.ClassRoomId)
                : db.Assignments.Where(x => x.CatechistId == id).Select(x => x.ClassRoomId);
            var labels = await (from classroom in db.Classes
                                join year in db.Years on classroom.AcademicYearId equals year.Id
                                where membership.Contains(classroom.Id) && (rights.Global || classIds.Contains(classroom.Id))
                                select new
                                {
                                    Classroom = classroom,
                                    Year = year
                                })
                .WhereIf(input.YearId != null, x => x.Year.Id == input.YearId)
                .Select(x => new { ClassName = x.Classroom.Name, YearName = x.Year.Name })
                .ToListAsync(ct);
            var className = string.Join(", ", labels.Select(x => x.ClassName).Distinct());
            var academicYearName = string.Join(", ", labels.Select(x => x.YearName).Distinct());
            if (input.Kind == "students")
            {
                var person = await db.Students.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (person == null || !rights.Global && !await db.Enrollments.AnyAsync(x => x.StudentId == id && classIds.Contains(x.ClassRoomId), ct))
                    throw new AppException(403, "CLASS_SCOPE", "Học sinh ngoài phạm vi.");
                foreach (var old in await db.Credentials.Where(x => x.StudentId == id && x.RevokedAtUtc == null).ToListAsync(ct))
                    old.RevokedAtUtc = now;
                var token = "CI1-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
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
                    id = person.Id,
                    fullName = person.FullName,
                    saintName = person.SaintName,
                    code = person.Code,
                    dateOfBirth = person.DateOfBirth,
                    credential = token
                });
            }
            else
            {
                var person = await db.Catechists.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (person == null || !rights.Global && !await db.Assignments.AnyAsync(x => x.CatechistId == id && classIds.Contains(x.ClassRoomId), ct))
                    throw new AppException(403, "CLASS_SCOPE", "GLV ngoài phạm vi.");
                foreach (var old in await db.CatechistCredentials.Where(x => x.CatechistId == id && x.RevokedAtUtc == null).ToListAsync(ct))
                    old.RevokedAtUtc = now;
                var token = "CG1-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                db.CatechistCredentials.Add(new()
                {
                    CatechistId = id,
                    TokenHash = AuthService.Hash(token),
                    IssuedAtUtc = now
                });
                rows.Add(new
                {
                    id = person.Id,
                    fullName = person.FullName,
                    saintName = person.SaintName,
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
            ActorId = actorId,
            EntityId = input.RequestId,
            EntityType = input.Kind,
            Action = "BatchCards",
            Detail = JsonSerializer.Serialize(new Cache(payload, protector.Protect(result)))
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return JsonSerializer.Deserialize<JsonElement>(result);
    }
}
