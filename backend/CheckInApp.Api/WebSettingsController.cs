using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;

[ApiController, Route("api/v1/web-settings")]
public class WebSettingsController(AppDbContext db, AccessService access, IWebHostEnvironment env) : ControllerBase
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    static readonly Guid SettingsId = Guid.Parse("814c43fe-86ec-4972-a217-9ab164c12123");
    record State(string Name, string? LogoFile, Guid Version, string? PreviousName = null, string? PreviousLogoFile = null);
    public record Input(string Name, string? LogoBase64, bool RemoveLogo, Guid Version);
    async Task<State> Read(CancellationToken ct)
    {
        var log = await db.Audit.Where(x => x.EntityId == SettingsId && x.Action == "UpdateWebSettings").OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        return log == null ? new("CheckInApp", null, Guid.Empty) : JsonSerializer.Deserialize<State>(log.Detail)!;
    }
    static object View(State s) => new { name = s.Name, logoUrl = s.LogoFile == null ? null : "/api/v1/web-settings/logo?v=" + s.Version };
    internal static void ValidatePng(byte[] data)
    {
        try
        {
            using var compressed = new MemoryStream();
            var offset = 8;
            var width = 0;
            var height = 0;
            var channels = 0;
            var ended = false;
            while (offset + 12 <= data.Length)
            {
                var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
                if (length < 0 || length > data.Length - offset - 12)
                    throw new InvalidDataException();
                var type = System.Text.Encoding.ASCII.GetString(data, offset + 4, 4);
                uint crc = 0xffffffff;
                for (var i = offset + 4; i < offset + 8 + length; i++)
                {
                    crc ^= data[i];
                    for (var b = 0; b < 8; b++)
                        crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
                }
                if ((crc ^ 0xffffffff) != System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8 + length, 4)))
                    throw new InvalidDataException();
                if (offset == 8 && type != "IHDR")
                    throw new InvalidDataException();
                if (type == "IHDR")
                {
                    if (offset != 8 || length != 13)
                        throw new InvalidDataException();
                    width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 8, 4));
                    height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 12, 4));
                    if (data[offset + 16] != 8 || data[offset + 18] != 0 || data[offset + 19] != 0 || data[offset + 20] != 0)
                        throw new InvalidDataException();
                    channels = data[offset + 17] switch
                    {
                        2 => 3,
                        6 => 4,
                        _ => throw new InvalidDataException()
                    };
                }
                if (type == "IDAT")
                    compressed.Write(data, offset + 8, length);
                offset += 12 + length;
                if (type == "IEND")
                {
                    if (length != 0 || offset != data.Length)
                        throw new InvalidDataException();
                    ended = true;
                    break;
                }
            }
            if (!ended || width < 1 || height < 1 || width > 2048 || height > 2048 || channels == 0)
                throw new InvalidDataException();
            compressed.Position = 0;
            using var inflated = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionMode.Decompress);
            var row = new byte[checked(width * channels + 1)];
            for (var i = 0; i < height; i++)
            {
                inflated.ReadExactly(row);
                if (row[0] > 4)
                    throw new InvalidDataException();
            }
            if (inflated.ReadByte() != -1)
                throw new InvalidDataException();
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or OverflowException) { throw new AppException(400, "LOGO_INVALID", "Ảnh PNG bị lỗi. Hãy chọn lại logo trong cửa sổ cấu hình."); }
    }
    [HttpGet, AllowAnonymous] public async Task<object> Public(CancellationToken ct) => View(await Read(ct));
    [HttpGet("admin"), Authorize]
    public async Task<object> Admin(CancellationToken ct)
    {
        var actor = Guid.Parse(User.FindFirst("sub")!.Value);
        var r = await access.Rights(actor, ct);
        if ((r.Mask & (long)(Permission.WebSettingsView | Permission.WebSettingsManage)) == 0)
            throw new AppException(403, "FORBIDDEN", "Không có quyền xem cấu hình web.");
        var s = await Read(ct);
        return new
        {
            s.Name,
            s.Version,
            logoUrl = s.LogoFile == null ? null : "/api/v1/web-settings/logo?v=" + s.Version
        };
    }
    [HttpGet("logo"), AllowAnonymous]
    public async Task<IActionResult> Logo(CancellationToken ct)
    {
        var s = await Read(ct);
        if (s.LogoFile == null)
            return NotFound();
        var path = Path.Combine(env.ContentRootPath, ".local", "web-logos", s.LogoFile);
        if (!System.IO.File.Exists(path))
            return NotFound();
        return PhysicalFile(path, "image/png");
    }
    [HttpPut, Authorize, RequestSizeLimit(3_000_000)]
    public async Task<object> Save(Input input, CancellationToken ct)
    {
        var actor = Guid.Parse(User.FindFirst("sub")!.Value);
        await access.Require(actor, Permission.WebSettingsManage, ct);
        var name = input.Name.Trim();
        if (name.Length is < 1 or > 100 || name.IndexOfAny(['<', '>']) >= 0)
            throw new AppException(400, "VALIDATION", "Tên web cần 1–100 ký tự, không chứa HTML.");
        byte[]? bytes = null;
        if (input.LogoBase64 != null)
        {
            try
            {
                bytes = Convert.FromBase64String(input.LogoBase64);
            }
            catch { throw new AppException(400, "LOGO_INVALID", "Ảnh không hợp lệ."); }
            if (bytes.Length is < 33 or > 2_000_000 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new AppException(400, "LOGO_INVALID", "Logo cần PNG tối đa 2 MB.");
            var w = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            var h = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            if (w < 1 || h < 1 || w > 2048 || h > 2048)
                throw new AppException(400, "LOGO_SIZE", "Logo tối đa 2048×2048 pixel.");
        }
        if (bytes != null)
            ValidatePng(bytes);
        await Gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var old = await Read(ct);
            if (old.Version != input.Version)
                throw new AppException(409, "VERSION_CONFLICT", "Cấu hình vừa thay đổi. Tải lại trước khi lưu.");
            var version = Guid.NewGuid();
            var file = input.RemoveLogo ? null : old.LogoFile;
            if (bytes != null)
            {
                file = version.ToString("N") + ".png";
                var directory = Path.Combine(env.ContentRootPath, ".local", "web-logos");
                Directory.CreateDirectory(directory);
                await System.IO.File.WriteAllBytesAsync(Path.Combine(directory, file), bytes, ct);
            }
            var state = new State(name, file, version, old.Name, old.LogoFile);
            db.Audit.Add(new()
            {
                Id = version,
                ActorId = actor,
                EntityId = SettingsId,
                EntityType = "WebSettings",
                Action = "UpdateWebSettings",
                Detail = JsonSerializer.Serialize(state)
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new
            {
                state.Name,
                state.Version,
                logoUrl = file == null ? null : "/api/v1/web-settings/logo?v=" + version
            };
        }
        finally { Gate.Release(); }
    }
}
