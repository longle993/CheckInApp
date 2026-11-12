using System.Text.Json;
using CheckInApp.Infrastructure;
using CheckInApp.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/dashboard-layout")]
public class DashboardController(AppDbContext db, AccessService access) : ControllerBase
{
    public record Cell(string Key, int X, int Y, int W, int H);
    public record Layout(string[] Widgets, string[] Shortcuts, Guid Version, Dictionary<string, Cell[]>? Grids = null);
    static readonly string[] WidgetKeys = ["greeting", "profile", "schedule", "shortcuts", "attendance"];
    static readonly string[] ShortcutKeys = ["lop", "hocsinh", "glv", "lich", "baocao", "admin"];
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    async Task<Layout> Read(CancellationToken ct)
    {
        var row = await db.Audit.Where(x => x.ActorId == Actor && x.Action == "DashboardLayout").OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        return row == null
            ? new(WidgetKeys, ["lop", "hocsinh", "glv", "baocao"], Guid.Empty)
            : JsonSerializer.Deserialize<Layout>(row.Detail)!;
    }
    static void ValidateGrids(Layout input)
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
                throw new CheckInApp.Application.AppException(400, "GRID_INVALID", "Vị trí hoặc kích thước widget không hợp lệ.");
            for (var i = 0; i < cells.Length; i++)
                for (var j = i + 1; j < cells.Length; j++)
                {
                    var a = cells[i];
                    var b = cells[j];
                    if (a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H)
                        throw new CheckInApp.Application.AppException(400, "GRID_OVERLAP", "Widget không được chồng nhau.");
                }
        }
    }
    async Task<Layout> Visible(Layout layout, CancellationToken ct)
    {
        var rights = await access.Rights(Actor, ct);
        var keys = layout.Widgets.Where(k => k is not ("attendance" or "schedule")
            || (rights.Mask & (long)Permission.AttendanceView) != 0
            && (k != "attendance" || (rights.Mask & (long)(Permission.StudentView | Permission.CatechistView)) != 0))
            .ToArray();
        return layout with
        {
            Widgets = keys,
            Grids = layout.Grids?.ToDictionary(x => x.Key, x => x.Value.Where(c => keys.Contains(c.Key)).ToArray())
        };
    }
    [HttpGet]
    public async Task<Layout> Get(CancellationToken ct)
    {
        return await Visible(await Read(ct), ct);
    }
    [HttpPut]
    public async Task<Layout> Put(Layout input, CancellationToken ct)
    {
        await access.Rights(Actor, ct);
        if (input.Widgets == null
            || input.Shortcuts == null
            || input.Widgets.Length > 5
            || input.Shortcuts.Length > 6
            || input.Widgets.Distinct().Count() != input.Widgets.Length
            || input.Shortcuts.Distinct().Count() != input.Shortcuts.Length
            || input.Widgets.Except(WidgetKeys).Any()
            || input.Shortcuts.Except(ShortcutKeys).Any())
            throw new CheckInApp.Application.AppException(400, "LAYOUT_INVALID", "Cấu hình tổng quan không hợp lệ.");
        if ((await Visible(input, ct)).Widgets.Length != input.Widgets.Length)
            throw new CheckInApp.Application.AppException(403, "WIDGET_FORBIDDEN", "Widget ngoài quyền xem.");
        ValidateGrids(input);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (db.Database.IsSqlServer())
            await db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={Actor}").LoadAsync(ct);
        var old = await Read(ct);
        if (old.Version != input.Version)
            throw new CheckInApp.Application.AppException(409, "VERSION_CONFLICT", "Cấu hình đã thay đổi. Hãy tải lại.");
        var next = input with
        {
            Version = Guid.NewGuid()
        };
        db.Audit.Add(new()
        {
            ActorId = Actor,
            EntityId = Actor,
            EntityType = "Dashboard",
            Action = "DashboardLayout",
            Detail = JsonSerializer.Serialize(next)
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return next;
    }
}
