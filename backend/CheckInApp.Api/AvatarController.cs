using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/people/{kind}/{id:guid}/avatar")]
public class AvatarController(AppDbContext db, AccessService access, IWebHostEnvironment env) : ControllerBase
{
    public record Input(string? PngBase64, bool Remove);
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    async Task Check(string kind, Guid id, bool write, CancellationToken ct)
    {
        if (kind is not ("students" or "catechists"))
            throw new AppException(404, "NOT_FOUND", "Loại hồ sơ không hợp lệ.");
        await access.Require(
            Actor,
            kind == "students"
            ? (write ? Permission.StudentManage : Permission.StudentView)
            : (write ? Permission.CatechistManage : Permission.CatechistView),
            ct);
        var rights = await access.Rights(Actor, ct);
        var ids = await access.ClassIds(Actor, ct);
        if (kind == "students")
        {
            if (!await db.Students.AnyAsync(x => x.Id == id, ct))
                throw new AppException(404, "NOT_FOUND", "Không có học sinh.");
            if (!rights.Global && !await db.Enrollments.AnyAsync(x => x.StudentId == id && ids.Contains(x.ClassRoomId), ct))
                throw new AppException(403, "CLASS_SCOPE", "Hồ sơ ngoài phạm vi.");
        }
        else
        {
            if (!await db.Catechists.AnyAsync(x => x.Id == id, ct))
                throw new AppException(404, "NOT_FOUND", "Không có GLV.");
            if (!rights.Global && !await db.Assignments.AnyAsync(x => x.CatechistId == id && ids.Contains(x.ClassRoomId), ct))
                throw new AppException(403, "CLASS_SCOPE", "Hồ sơ ngoài phạm vi.");
        }
    }
    [HttpGet]
    public async Task<IActionResult> Get(string kind, Guid id, CancellationToken ct)
    {
        await Check(kind, id, false, ct);
        Response.Headers.CacheControl = "private, no-store";
        var row = await db.Audit.Where(x => x.EntityId == id && x.EntityType == kind && x.Action == "Avatar").OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (row == null || string.IsNullOrEmpty(row.Detail))
            return NotFound();
        var path = Path.Combine(env.ContentRootPath, ".local", "avatars", row.Detail);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/png") : NotFound();
    }
    [HttpPut, RequestSizeLimit(3_000_000)]
    public async Task<IActionResult> Put(string kind, Guid id, Input input, CancellationToken ct)
    {
        await Check(kind, id, true, ct);
        string file = "";
        if (!input.Remove)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(input.PngBase64 ?? "");
            }
            catch (FormatException) { throw new AppException(400, "IMAGE_INVALID", "Ảnh không hợp lệ."); }
            if (bytes.Length > 2_000_000
                || bytes.Length < 24
                || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new AppException(400, "IMAGE_INVALID", "Chọn ảnh PNG tối đa 2 MB.");
            WebSettingsController.ValidatePng(bytes);
            file = Guid.NewGuid().ToString("N") + ".png";
            var dir = Path.Combine(env.ContentRootPath, ".local", "avatars");
            Directory.CreateDirectory(dir);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(dir, file), bytes, ct);
        }
        db.Audit.Add(new()
        {
            ActorId = Actor,
            EntityId = id,
            EntityType = kind,
            Action = "Avatar",
            Detail = file
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
