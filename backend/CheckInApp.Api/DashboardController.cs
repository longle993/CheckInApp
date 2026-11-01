using System.Text.Json;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;
[ApiController,Authorize,Route("api/v1/dashboard-layout")]
public class DashboardController(AppDbContext db,AccessService access):ControllerBase
{
    public record Layout(string[] Widgets,string[] Shortcuts,Guid Version);
    static readonly string[] WidgetKeys=["greeting","profile","schedule","shortcuts"];
    static readonly string[] ShortcutKeys=["lop","hocsinh","glv","lich","baocao","admin"];
    Guid Actor=>Guid.Parse(User.FindFirst("sub")!.Value);
    async Task<Layout> Read(CancellationToken ct){var row=await db.Audit.Where(x=>x.ActorId==Actor&&x.Action=="DashboardLayout").OrderByDescending(x=>x.CreatedAtUtc).ThenByDescending(x=>x.Id).FirstOrDefaultAsync(ct);return row==null?new(WidgetKeys,["lop","hocsinh","glv","baocao"],Guid.Empty):JsonSerializer.Deserialize<Layout>(row.Detail)!;}
    [HttpGet] public async Task<Layout> Get(CancellationToken ct){await access.Rights(Actor,ct);return await Read(ct);}
    [HttpPut] public async Task<Layout> Put(Layout input,CancellationToken ct){await access.Rights(Actor,ct);if(input.Widgets==null||input.Shortcuts==null||input.Widgets.Length>4||input.Shortcuts.Length>6||input.Widgets.Distinct().Count()!=input.Widgets.Length||input.Shortcuts.Distinct().Count()!=input.Shortcuts.Length||input.Widgets.Except(WidgetKeys).Any()||input.Shortcuts.Except(ShortcutKeys).Any())throw new CheckInApp.Application.AppException(400,"LAYOUT_INVALID","Cấu hình tổng quan không hợp lệ.");await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);if(db.Database.IsSqlServer())await db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={Actor}").LoadAsync(ct);var old=await Read(ct);if(old.Version!=input.Version)throw new CheckInApp.Application.AppException(409,"VERSION_CONFLICT","Cấu hình đã thay đổi. Hãy tải lại.");var next=input with{Version=Guid.NewGuid()};db.Audit.Add(new(){ActorId=Actor,EntityId=Actor,EntityType="Dashboard",Action="DashboardLayout",Detail=JsonSerializer.Serialize(next)});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return next;}
}
