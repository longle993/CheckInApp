using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheckInApp.Infrastructure;

public class DashboardService(AppDbContext db, AccessService access) : IDashboardService
{
    static readonly string[] WidgetKeys = ["greeting", "profile", "schedule", "shortcuts", "attendance"];
    static readonly string[] ShortcutKeys = ["lop", "hocsinh", "glv", "lich", "baocao", "admin"];

    async Task<DashboardLayout> Read(Guid actorId, CancellationToken ct)
    {
        var row = await db.Audit
            .Where(x => x.ActorId == actorId && x.Action == "DashboardLayout")
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        return row == null
            ? new(WidgetKeys, ["lop", "hocsinh", "glv", "baocao"], Guid.Empty)
            : JsonSerializer.Deserialize<DashboardLayout>(row.Detail)!;
    }

    static void ValidateGrids(DashboardLayout input)
    {
        if (input.Grids == null)
            return;
        foreach (var (key, cells) in input.Grids)
        {
            var columns = key switch
            {
                "desktop" => 12,
                "tablet" => 6,
                "mobile" => 2,
                _ => 0
            };
            if (columns == 0
                || cells == null
                || cells.Length != input.Widgets.Length
                || cells.Select(x => x.Key).Distinct().Count() != cells.Length
                || cells.Any(x => !input.Widgets.Contains(x.Key)
                    || x.X < 0
                    || x.Y < 0
                    || x.Y > 100
                    || x.W < 1
                    || x.H < 1
                    || x.H > 20
                    || x.X + x.W > columns
                    || x.Key == "attendance" && (x.W < Math.Min(6, columns) || x.H < 6)))
                throw new AppException(400, "GRID_INVALID", "Vị trí hoặc kích thước widget không hợp lệ.");
            for (var i = 0; i < cells.Length; i++)
                for (var j = i + 1; j < cells.Length; j++)
                {
                    var a = cells[i];
                    var b = cells[j];
                    if (a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H)
                        throw new AppException(400, "GRID_OVERLAP", "Widget không được chồng nhau.");
                }
        }
    }

    async Task<DashboardLayout> Visible(Guid actorId, DashboardLayout layout, CancellationToken ct)
    {
        var rights = await access.Rights(actorId, ct);
        var keys = layout.Widgets
            .Where(key => key is not ("attendance" or "schedule")
                || (rights.Mask & (long)Permission.AttendanceView) != 0
                && (key != "attendance" || (rights.Mask & (long)(Permission.StudentView | Permission.CatechistView)) != 0))
            .ToArray();
        return layout with
        {
            Widgets = keys,
            Grids = layout.Grids?.ToDictionary(x => x.Key, x => x.Value.Where(cell => keys.Contains(cell.Key)).ToArray())
        };
    }

    public async Task<DashboardLayout> Get(Guid actorId, CancellationToken ct) =>
        await Visible(actorId, await Read(actorId, ct), ct);

    public async Task<DashboardLayout> Save(Guid actorId, DashboardLayout input, CancellationToken ct)
    {
        await access.Rights(actorId, ct);
        if (input.Widgets == null
            || input.Shortcuts == null
            || input.Widgets.Length > 5
            || input.Shortcuts.Length > 6
            || input.Widgets.Distinct().Count() != input.Widgets.Length
            || input.Shortcuts.Distinct().Count() != input.Shortcuts.Length
            || input.Widgets.Except(WidgetKeys).Any()
            || input.Shortcuts.Except(ShortcutKeys).Any())
            throw new AppException(400, "LAYOUT_INVALID", "Cấu hình tổng quan không hợp lệ.");
        if ((await Visible(actorId, input, ct)).Widgets.Length != input.Widgets.Length)
            throw new AppException(403, "WIDGET_FORBIDDEN", "Widget ngoài quyền xem.");
        ValidateGrids(input);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (db.Database.IsSqlServer())
            await db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actorId}").LoadAsync(ct);
        var old = await Read(actorId, ct);
        if (old.Version != input.Version)
            throw new AppException(409, "VERSION_CONFLICT", "Cấu hình đã thay đổi. Hãy tải lại.");
        var next = input with
        {
            Version = Guid.NewGuid()
        };
        db.Audit.Add(new()
        {
            ActorId = actorId,
            EntityId = actorId,
            EntityType = "Dashboard",
            Action = "DashboardLayout",
            Detail = JsonSerializer.Serialize(next)
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return next;
    }
}
