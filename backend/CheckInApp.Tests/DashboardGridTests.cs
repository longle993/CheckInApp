using CheckInApp.Application;
using CheckInApp.Infrastructure;
using Xunit;
namespace CheckInApp.Tests;

public class DashboardGridTests
{
    static DashboardService Service(Fixture f) => new(f.Db, f.Access);
    [Fact]
    public async Task GridSaveRejectsOverlapAndConflictingVersion()
    {
        await using var f = new Fixture();
        await f.Init();
        var service = Service(f);
        var original = await service.Get(f.User, default);
        var bad = new DashboardLayout(["greeting", "profile"], [], original.Version, new()
        {
            ["mobile"] = [new("greeting", 0, 0, 2, 2), new("profile", 0, 1, 2, 3)]
        });
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => service.Save(f.User, bad, default))).Status);
        var good = bad with
        {
            Grids = new()
            {
                ["mobile"] = [new("greeting", 0, 0, 2, 2), new("profile", 0, 2, 2, 3)]
            }
        };
        var saved = await service.Save(f.User, good, default);
        Assert.NotEqual(original.Version, saved.Version);
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => service.Save(f.User, good, default))).Status);
    }
    [Fact]
    public async Task CalendarWidgetRequiresPersonViewPermission()
    {
        await using var f = new Fixture();
        await f.Init();
        var service = Service(f);
        var layout = await service.Get(f.Teacher, default);
        Assert.DoesNotContain("attendance", layout.Widgets);
        var error = await Assert.ThrowsAsync<AppException>(() => service.Save(f.Teacher, new(["attendance"], [], layout.Version), default));
        Assert.Equal(403, error.Status);
    }
}
