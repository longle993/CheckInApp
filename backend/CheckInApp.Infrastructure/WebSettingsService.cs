using System.Buffers.Binary;
using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CheckInApp.Infrastructure;

public class WebSettingsService(AppDbContext db, AccessService access, IWebHostEnvironment environment) : IWebSettingsService
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    static readonly Guid SettingsId = Guid.Parse("814c43fe-86ec-4972-a217-9ab164c12123");
    record State(string Name, string? LogoFile, Guid Version, string? PreviousName = null, string? PreviousLogoFile = null);

    async Task<State> Read(CancellationToken ct)
    {
        var log = await db.Audit
            .Where(x => x.EntityId == SettingsId && x.Action == "UpdateWebSettings")
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        return log == null ? new("CheckInApp", null, Guid.Empty) : JsonSerializer.Deserialize<State>(log.Detail)!;
    }

    static object View(State state) => new
    {
        name = state.Name,
        logoUrl = state.LogoFile == null ? null : "/api/v1/web-settings/logo?v=" + state.Version
    };

    public async Task<object> Public(CancellationToken ct) => View(await Read(ct));

    public async Task<object> Admin(Guid actorId, CancellationToken ct)
    {
        var rights = await access.Rights(actorId, ct);
        if ((rights.Mask & (long)(Permission.WebSettingsView | Permission.WebSettingsManage)) == 0)
            throw new AppException(403, "FORBIDDEN", "Không có quyền xem cấu hình web.");
        var state = await Read(ct);
        return new
        {
            state.Name,
            state.Version,
            logoUrl = state.LogoFile == null ? null : "/api/v1/web-settings/logo?v=" + state.Version
        };
    }

    public async Task<StoredFile?> Logo(CancellationToken ct)
    {
        var state = await Read(ct);
        if (state.LogoFile == null)
            return null;
        var path = Path.Combine(environment.ContentRootPath, ".local", "web-logos", state.LogoFile);
        return File.Exists(path) ? new(path, "image/png") : null;
    }

    public async Task<object> Save(Guid actorId, WebSettingsInput input, CancellationToken ct)
    {
        await access.Require(actorId, Permission.WebSettingsManage, ct);
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
            catch
            {
                throw new AppException(400, "LOGO_INVALID", "Ảnh không hợp lệ.");
            }
            if (bytes.Length is < 33 or > 2_000_000 || !bytes.AsSpan(0, 8).SequenceEqual(PngValidator.Signature))
                throw new AppException(400, "LOGO_INVALID", "Logo cần PNG tối đa 2 MB.");
            var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            if (width < 1 || height < 1 || width > 2048 || height > 2048)
                throw new AppException(400, "LOGO_SIZE", "Logo tối đa 2048×2048 pixel.");
            PngValidator.Validate(bytes);
        }
        await Gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var old = await Read(ct);
            if (old.Version != input.Version)
                throw new AppException(409, "VERSION_CONFLICT", "Cấu hình vừa thay đổi. Tải lại trước khi lưu.");
            var version = Guid.NewGuid();
            var file = input.RemoveLogo ? null : old.LogoFile;
            if (bytes != null)
            {
                file = version.ToString("N") + ".png";
                var directory = Path.Combine(environment.ContentRootPath, ".local", "web-logos");
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(Path.Combine(directory, file), bytes, ct);
            }
            var state = new State(name, file, version, old.Name, old.LogoFile);
            db.Audit.Add(new()
            {
                Id = version,
                ActorId = actorId,
                EntityId = SettingsId,
                EntityType = "WebSettings",
                Action = "UpdateWebSettings",
                Detail = JsonSerializer.Serialize(state)
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new
            {
                state.Name,
                state.Version,
                logoUrl = file == null ? null : "/api/v1/web-settings/logo?v=" + version
            };
        }
        finally
        {
            Gate.Release();
        }
    }
}
