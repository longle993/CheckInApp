using System.Security.Claims;
using System.Text.Json;
using CheckInApp.Api;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;

public class CalendarTests
{
    [Theory]
    [InlineData(-30, -10, null, false, "absent")]
    [InlineData(-10, 20, null, false, "pending")]
    [InlineData(10, 30, null, false, "future")]
    [InlineData(-30, -10, -30, false, "ontime")]
    [InlineData(-30, -10, -29, false, "late")]
    [InlineData(-30, -10, null, true, "cancelled")]
    [InlineData(-30, -10, int.MinValue, false, "unknown")]
    public async Task StatusUsesEachSessionAndNeverMarksFutureAbsent(int start, int end, int? entered, bool cancelled, string expected)
    {
        await using var f = new Fixture();
        await f.Init();
        var session = await f.Db.Sessions.SingleAsync();
        var now = f.Clock.GetUtcNow().UtcDateTime;
        session.OpensAtUtc = now.AddMinutes(start);
        session.ClosesAtUtc = now.AddMinutes(end);
        session.Cancelled = cancelled;
        if (entered != null)
            f.Db.Attendance.Add(new()
            {
                SessionId = f.Session,
                StudentId = f.Student,
                RecordedAtUtc = entered == int.MinValue ? DateTime.MinValue : now.AddMinutes(entered.Value)
            });
        await f.Db.SaveChangesAsync();
        var controller = new AttendanceCalendarController(f.Db, f.Access, f.Clock) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", f.User.ToString())], "test")) } } };
        var result = JsonSerializer.SerializeToElement(await controller.Get((await f.Db.Years.SingleAsync()).Id, "students", f.Student, 2026, 10, null, default));
        Assert.Equal(expected, result.GetProperty("items")[0].GetProperty("status").GetString());
    }
    [Fact]
    public async Task CalendarRejectsOutOfScopePerson()
    {
        await using var f = new Fixture();
        await f.Init();
        var controller = new AttendanceCalendarController(f.Db, f.Access, f.Clock) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", f.User.ToString())], "test")) } } };
        var e = await Assert.ThrowsAsync<AppException>(() => controller.Get((f.Db.Years.Single()).Id, "students", Guid.NewGuid(), 2026, 10, null, default));
        Assert.Equal(403, e.Status);
    }
    [Fact]
    public async Task ThreeSessionsInOneDayKeepIndependentStatuses()
    {
        await using var f = new Fixture();
        await f.Init();
        var now = f.Clock.GetUtcNow().UtcDateTime;
        foreach (var hour in new[] { 1, 3 })
        {
            var s = new AttendanceSession { ClassRoomId = f.ClassA, Date = new(2026, 10, 25), OpensAtUtc = now.Date.AddHours(hour), ClosesAtUtc = now.Date.AddHours(hour + 1) };
            f.Db.Sessions.Add(s);
            f.Db.Rosters.Add(new()
            {
                SessionId = s.Id,
                StudentId = f.Student,
                EnrollmentId = f.Enrollment
            });
            if (hour == 1)
                f.Db.Attendance.Add(new()
                {
                    SessionId = s.Id,
                    StudentId = f.Student,
                    RecordedAtUtc = s.OpensAtUtc.AddMinutes(5)
                });
        }
        f.Db.Attendance.Add(new()
        {
            SessionId = f.Session,
            StudentId = f.Student,
            RecordedAtUtc = now.Date.AddHours(2)
        });
        await f.Db.SaveChangesAsync();
        var controller = new AttendanceCalendarController(f.Db, f.Access, f.Clock) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", f.User.ToString())], "test")) } } };
        var result = JsonSerializer.SerializeToElement(await controller.Get((await f.Db.Years.SingleAsync()).Id, "students", f.Student, 2026, 10, null, default));
        Assert.Equal(
            new[] { "late", "ontime", "future" },
            result.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("status").GetString()).ToArray());
    }
}
