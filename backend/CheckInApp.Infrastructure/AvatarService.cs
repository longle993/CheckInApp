using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CheckInApp.Infrastructure;

public class AvatarService(AppDbContext db, AccessService access, IWebHostEnvironment environment) : IAvatarService
{
    async Task Check(Guid actorId, string kind, Guid personId, bool write, CancellationToken ct)
    {
        if (kind is not ("students" or "catechists"))
            throw new AppException(404, "NOT_FOUND", "Loại hồ sơ không hợp lệ.");
        await access.Require(
            actorId,
            kind == "students"
                ? write ? Permission.StudentManage : Permission.StudentView
                : write ? Permission.CatechistManage : Permission.CatechistView,
            ct);
        var rights = await access.Rights(actorId, ct);
        var classIds = await access.ClassIds(actorId, ct);
        if (kind == "students")
        {
            if (!await db.Students.AnyAsync(x => x.Id == personId, ct))
                throw new AppException(404, "NOT_FOUND", "Không có học sinh.");
            if (!rights.Global && !await db.Enrollments.AnyAsync(x => x.StudentId == personId && classIds.Contains(x.ClassRoomId), ct))
                throw new AppException(403, "CLASS_SCOPE", "Hồ sơ ngoài phạm vi.");
        }
        else
        {
            if (!await db.Catechists.AnyAsync(x => x.Id == personId, ct))
                throw new AppException(404, "NOT_FOUND", "Không có GLV.");
            if (!rights.Global && !await db.Assignments.AnyAsync(x => x.CatechistId == personId && classIds.Contains(x.ClassRoomId), ct))
                throw new AppException(403, "CLASS_SCOPE", "Hồ sơ ngoài phạm vi.");
        }
    }

    public async Task<StoredFile?> Get(Guid actorId, string kind, Guid personId, CancellationToken ct)
    {
        await Check(actorId, kind, personId, false, ct);
        var row = await db.Audit
            .Where(x => x.EntityId == personId && x.EntityType == kind && x.Action == "Avatar")
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (row == null || string.IsNullOrEmpty(row.Detail))
            return null;
        var path = Path.Combine(environment.ContentRootPath, ".local", "avatars", row.Detail);
        return File.Exists(path) ? new(path, "image/png") : null;
    }

    public async Task Save(Guid actorId, string kind, Guid personId, AvatarInput input, CancellationToken ct)
    {
        await Check(actorId, kind, personId, true, ct);
        var file = "";
        if (!input.Remove)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(input.PngBase64 ?? "");
            }
            catch (FormatException)
            {
                throw new AppException(400, "IMAGE_INVALID", "Ảnh không hợp lệ.");
            }
            if (bytes.Length > 2_000_000
                || bytes.Length < 24
                || !bytes.AsSpan(0, 8).SequenceEqual(PngValidator.Signature))
                throw new AppException(400, "IMAGE_INVALID", "Chọn ảnh PNG tối đa 2 MB.");
            PngValidator.Validate(bytes);
            file = Guid.NewGuid().ToString("N") + ".png";
            var directory = Path.Combine(environment.ContentRootPath, ".local", "avatars");
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, file), bytes, ct);
        }
        db.Audit.Add(new()
        {
            ActorId = actorId,
            EntityId = personId,
            EntityType = kind,
            Action = "Avatar",
            Detail = file
        });
        await db.SaveChangesAsync(ct);
    }
}
