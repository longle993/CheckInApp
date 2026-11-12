using System.Security.Claims;
using CheckInApp.Api;
using CheckInApp.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;
namespace CheckInApp.Tests;

public class DashboardGridTests
{
    static DashboardController Controller(Fixture f, Guid actor) => new(f.Db, f.Access) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actor.ToString())], "test")) } } };
    [Fact]
    public async Task GridSaveRejectsOverlapAndConflictingVersion()
    {
        await using var f = new Fixture();
        await f.Init();
        var c = Controller(f, f.User);
        var original = await c.Get(default);
        var bad = new DashboardController.Layout(["greeting", "profile"], [], original.Version, new()
        {
            ["mobile"] = [new("greeting", 0, 0, 2, 2), new("profile", 0, 1, 2, 3)]
        });
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => c.Put(bad, default))).Status);
        var good = bad with
        {
            Grids = new()
            {
                ["mobile"] = [new("greeting", 0, 0, 2, 2), new("profile", 0, 2, 2, 3)]
            }
        };
        var saved = await c.Put(good, default);
        Assert.NotEqual(original.Version, saved.Version);
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => c.Put(good, default))).Status);
    }
    [Fact]
    public async Task CalendarWidgetRequiresPersonViewPermission()
    {
        await using var f = new Fixture();
        await f.Init();
        var c = Controller(f, f.Teacher);
        var layout = await c.Get(default);
        Assert.DoesNotContain("attendance", layout.Widgets);
        var error = await Assert.ThrowsAsync<AppException>(() => c.Put(new(["attendance"], [], layout.Version), default));
        Assert.Equal(403, error.Status);
    }
}
