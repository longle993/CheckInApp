using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;

public class BulkDeleteTests
{
    static BulkDeleteService Service(Fixture f) => new(f.Db, f.Access, new(f.Db, f.Clock));
    [Fact]
    public async Task StudentDeleteRemovesDependentsAndRetryButPreservesClassAndUser()
    {
        await using var f = new Fixture();
        await f.Init();
        await f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "delete-test-0001")), default);
        var request = new DeleteSelection("students", [f.Student]);
        var preview = await Service(f).Run(f.User, request, false);
        Assert.True(await f.Db.Students.AnyAsync());
        Assert.Equal(1, preview.Counts["Điểm danh HS"]);
        await Service(f).Run(f.User, request with
        {
            Fingerprint = preview.Fingerprint
        }, true);
        Assert.False(await f.Db.Students.AnyAsync());
        Assert.False(await f.Db.Attendance.AnyAsync());
        Assert.False(await f.Db.Rosters.AnyAsync());
        Assert.False(await f.Db.Credentials.AnyAsync());
        Assert.False(await f.Db.Idempotency.AnyAsync());
        Assert.Equal(2, await f.Db.Classes.CountAsync());
        Assert.Equal(2, await f.Db.Users.CountAsync());
    }
    [Fact]
    public async Task ChangedDataRejectsWholeBatch()
    {
        await using var f = new Fixture();
        await f.Init();
        var request = new DeleteSelection("students", [f.Student]);
        var preview = await Service(f).Run(f.User, request, false);
        await f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "delete-test-0002")), default);
        var error = await Assert.ThrowsAsync<AppException>(() => Service(f).Run(f.User, request with { Fingerprint = preview.Fingerprint }, true));
        Assert.Equal(409, error.Status);
        Assert.True(await f.Db.Students.AnyAsync());
        Assert.True(await f.Db.Attendance.AnyAsync());
    }
    [Fact]
    public async Task YearDeletePreservesPeopleAndAccounts()
    {
        await using var f = new Fixture();
        await f.Init();
        var year = await f.Db.Years.SingleAsync();
        var request = new DeleteSelection("years", [year.Id]);
        var preview = await Service(f).Run(f.User, request, false);
        await Service(f).Run(f.User, request with
        {
            Fingerprint = preview.Fingerprint
        }, true);
        Assert.False(await f.Db.Classes.AnyAsync());
        Assert.False(await f.Db.Sessions.AnyAsync());
        Assert.True(await f.Db.Students.AnyAsync());
        Assert.True(await f.Db.Catechists.AnyAsync());
        Assert.Equal(2, await f.Db.Users.CountAsync());
    }
    [Fact]
    public async Task ScopedManagerCannotDeletePersonWithOtherClassHistory()
    {
        await using var f = new Fixture();
        await f.Init();
        var role = await f.Db.Roles.SingleAsync(x => x.Name == "Teacher");
        role.Permissions |= (long)Permission.StudentManage;
        f.Db.Enrollments.Add(new()
        {
            StudentId = f.Student,
            ClassRoomId = f.ClassB,
            From = new(2025, 1, 1),
            Until = new(2025, 2, 1)
        });
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<AppException>(() => Service(f).Run(f.Teacher, new("students", [f.Student]), false));
        Assert.Equal(403, error.Status);
        Assert.True(await f.Db.Students.AnyAsync());
    }
    [Fact]
    public async Task MissingSelectionDoesNotDeleteExistingRows()
    {
        await using var f = new Fixture();
        await f.Init();
        await Assert.ThrowsAsync<AppException>(() => Service(f).Run(f.User, new("students", [f.Student, Guid.NewGuid()]), true));
        Assert.True(await f.Db.Students.AnyAsync());
    }
    [Fact]
    public async Task DeletedOccurrenceDoesNotReturnAfterSynchronization()
    {
        await using var f = new Fixture();
        await f.Init();
        var frame = new AttendanceSchedule { ClassRoomId = f.ClassA, From = new(2026, 11, 2), Until = new(2026, 11, 3), DaysMask = 127, OpensAt = new(9, 0), ClosesAt = new(10, 0) };
        f.Db.Schedules.Add(frame);
        await f.Db.SaveChangesAsync();
        var schedule = new ScheduleService(f.Db, f.Clock);
        await schedule.Synchronize(f.ClassA);
        var session = await f.Db.Sessions.SingleAsync(x => x.ScheduleId == frame.Id && x.Date == frame.From);
        var request = new DeleteSelection("sessions", [session.Id]);
        var preview = await Service(f).Run(f.User, request, false);
        await Service(f).Run(f.User, request with
        {
            Fingerprint = preview.Fingerprint
        }, true);
        await schedule.Synchronize(f.ClassA);
        Assert.False(await f.Db.Sessions.AnyAsync(x => x.ScheduleId == frame.Id && x.Date == frame.From));
        Assert.True(await f.Db.Sessions.AnyAsync(x => x.ScheduleId == frame.Id && x.Date == frame.Until));
    }
    [Fact]
    public async Task TeacherDeleteKeepsLinkedLogin()
    {
        await using var f = new Fixture();
        await f.Init();
        var teacher = await f.Db.Catechists.SingleAsync();
        f.Db.CatechistAttendance.Add(new()
        {
            CatechistId = teacher.Id,
            SessionId = f.Session,
            CheckedInAtUtc = f.Clock.GetUtcNow().UtcDateTime
        });
        await f.Db.SaveChangesAsync();
        var request = new DeleteSelection("catechists", [teacher.Id]);
        var preview = await Service(f).Run(f.User, request, false);
        await Service(f).Run(f.User, request with
        {
            Fingerprint = preview.Fingerprint
        }, true);
        Assert.True(await f.Db.Users.AnyAsync(x => x.Id == f.Teacher));
        Assert.False(await f.Db.CatechistAttendance.AnyAsync());
        Assert.False(await f.Db.Assignments.AnyAsync());
    }
}
