using System.Security.Claims;
using CheckInApp.Api;
using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;

public class RefinementTests
{
    static AccountManagementController Controller(Fixture f) => new(f.Db, f.Access) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", f.User.ToString())], "test")) } } };
    [Fact]
    public async Task AdminCanLinkTeacherWithoutRoleChange()
    {
        await using var f = new Fixture();
        await f.Init();
        var teacher = new Catechist { FullName = "Admin GLV" };
        f.Db.Catechists.Add(teacher);
        await f.Db.SaveChangesAsync();
        var u = await f.Db.Users.SingleAsync(x => x.Id == f.User);
        await Controller(f).Edit(
            u.Id,
            new(
            u.FullName,
            u.Email,
            "Liên kết",
            u.FullName,
            u.Email,
            CatechistId: teacher.Id,
            UpdateCatechistLink: true),
            default);
        Assert.Equal(u.Id, teacher.UserId);
        Assert.Equal("Admin", (await f.Db.Roles.SingleAsync(r => r.Name == "Admin")).Name);
    }
    [Fact]
    public async Task ExistingTeacherLinkCannotBeStolen()
    {
        await using var f = new Fixture();
        await f.Init();
        var teacher = await f.Db.Catechists.SingleAsync();
        var u = await f.Db.Users.SingleAsync(x => x.Id == f.User);
        var e = await Assert.ThrowsAsync<AppException>(() => Controller(f).Edit(
            u.Id,
            new(
            u.FullName,
            u.Email,
            "Liên kết",
            u.FullName,
            u.Email,
            CatechistId: teacher.Id,
            UpdateCatechistLink: true),
            default));
        Assert.Equal(409, e.Status);
        Assert.Equal(f.Teacher, teacher.UserId);
    }
    [Fact]
    public async Task EditPermissionDoesNotGrantDelete()
    {
        await using var f = new Fixture();
        await f.Init();
        var role = await f.Db.Roles.SingleAsync(r => r.Name == "Admin");
        role.Permissions &= ~(long)Permission.StudentDelete;
        await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AppException>(() => new BulkDeleteService(f.Db, f.Access, new(f.Db, f.Clock)).Run(f.User, new("students", [f.Student]), false));
        Assert.Equal(403, e.Status);
        Assert.True(await f.Db.Students.AnyAsync());
    }
    [Fact]
    public async Task ClassDeletePreservesPeople()
    {
        await using var f = new Fixture();
        await f.Init();
        var service = new BulkDeleteService(f.Db, f.Access, new(f.Db, f.Clock));
        var request = new DeleteSelection("classes", [f.ClassA]);
        var p = await service.Run(f.User, request, false);
        await service.Run(f.User, request with
        {
            Fingerprint = p.Fingerprint
        }, true);
        Assert.False(await f.Db.Classes.AnyAsync(c => c.Id == f.ClassA));
        Assert.True(await f.Db.Students.AnyAsync(c => c.Id == f.Student));
        Assert.True(await f.Db.Users.AnyAsync(c => c.Id == f.User));
    }

    [Fact]
    public async Task MovingIntoDateWithAttendanceWarns()
    {
        await using var f = new Fixture();
        await f.Init();
        await f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "target-date-check")), default);
        var future = new AttendanceSession { ClassRoomId = f.ClassA, Date = new(2026, 10, 26), OpensAtUtc = new(2026, 10, 26, 2, 0, 0), ClosesAtUtc = new(2026, 10, 26, 3, 0, 0) };
        f.Db.Sessions.Add(future);
        await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AppException>(() => new ScheduleService(f.Db, f.Clock).EditSession(
            f.User,
            future.Id,
            new(new(12, 0), new(13, 0), true, true, future.Version, "Dời", new(2026, 10, 25)),
            f.Access,
            default));
        Assert.Equal("TARGET_DATE_HAS_DATA", e.Code);
        Assert.Equal(new DateOnly(2026, 10, 26), future.Date);
    }
    [Fact]
    public async Task CancelledUnusedFutureSessionIsRemovedAndNotRegenerated()
    {
        await using var f = new Fixture();
        await f.Init();
        var frame = new AttendanceSchedule { ClassRoomId = f.ClassA, From = new(2026, 10, 26), Until = new(2026, 10, 26), DaysMask = 127, OpensAt = new(9, 0), ClosesAt = new(10, 0) };
        f.Db.Schedules.Add(frame);
        await f.Db.SaveChangesAsync();
        var svc = new ScheduleService(f.Db, f.Clock);
        await svc.Synchronize(f.ClassA);
        var id = (await f.Db.Sessions.SingleAsync(x => x.ScheduleId == frame.Id)).Id;
        f.Db.Exceptions.Add(new()
        {
            ClassRoomId = f.ClassA,
            ScheduleId = frame.Id,
            Date = frame.From,
            Cancelled = true
        });
        await f.Db.SaveChangesAsync();
        await svc.Synchronize(f.ClassA);
        await svc.Synchronize(f.ClassA);
        Assert.False(await f.Db.Sessions.AnyAsync(x => x.Id == id));
        Assert.False(await f.Db.Rosters.AnyAsync(x => x.SessionId == id));
    }
}
