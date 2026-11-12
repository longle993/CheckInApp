using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;

public class ExtensionTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ScheduleSnapshotsTargetsAndKeepsOpenSessions(bool students, bool teachers)
    {
        await using var f = new Fixture();
        await f.Init();
        f.Db.Schedules.Add(new()
        {
            ClassRoomId = f.ClassA,
            From = new(2026, 10, 25),
            Until = new(2026, 10, 26),
            DaysMask = 127,
            OpensAt = new(9, 0),
            ClosesAt = new(10, 0),
            StudentAttendanceEnabled = students,
            CatechistAttendanceEnabled = teachers
        });
        await f.Db.SaveChangesAsync();
        await new ScheduleService(f.Db, f.Clock).Synchronize(f.ClassA);
        f.Db.ChangeTracker.Clear();
        var future = await f.Db.Sessions.SingleAsync(x => x.Date == new DateOnly(2026, 10, 26));
        Assert.Equal(students, future.StudentAttendanceEnabled);
        Assert.Equal(teachers, future.CatechistAttendanceEnabled);
        Assert.Equal(students ? 1 : 0, await f.Db.Rosters.CountAsync(x => x.SessionId == future.Id));
        Assert.True((await f.Db.Sessions.SingleAsync(x => x.Id == f.Session)).StudentAttendanceEnabled);
    }
    [Fact]
    public async Task DisabledTargetsAreRejectedByServer()
    {
        await using var f = new Fixture();
        await f.Init();
        var session = await f.Db.Sessions.SingleAsync();
        session.StudentAttendanceEnabled = false;
        session.CatechistAttendanceEnabled = false;
        await f.Db.SaveChangesAsync();
        var teacher = await f.Db.Catechists.SingleAsync();
        var presence = new PresenceService(f.Db, f.Access, f.Clock);
        Assert.Equal(
            409,
            (await Assert.ThrowsAsync<AppException>(() => presence.Save(
            f.User,
            f.Session,
            teacher.Id,
            true,
            new(f.Clock.GetUtcNow().UtcDateTime, null, null, "Test"),
            default))).Status);
        Assert.Equal(
            409,
            (await Assert.ThrowsAsync<AppException>(() => f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "disabled-0001")), default))).Status);
        f.Db.ChangeTracker.Clear();
        Assert.False((await f.Db.Sessions.SingleAsync()).StudentAttendanceEnabled);
    }
    [Fact]
    public void ExcelRoundTripPreservesVietnameseAndLiteralFormulaText()
    {
        var rows = ExcelFile.Read(ExcelFile.Write([["id", "fullName", "dateOfBirth"], ["", "Maria Nguyễn Ánh =1+1", "2016-01-02"]]));
        Assert.Equal("Maria Nguyễn Ánh =1+1", rows[0]["fullName"]);
        Assert.Equal("2016-01-02", rows[0]["dateOfBirth"]);
        Assert.Throws<AppException>(() => ExcelFile.Read([1, 2, 3]));
    }
    [Fact]
    public async Task TeacherPresenceEnforcesPermissionAssignmentVersionAndChronology()
    {
        await using var f = new Fixture();
        await f.Init();
        var service = new PresenceService(f.Db, f.Access, f.Clock);
        var teacher = await f.Db.Catechists.SingleAsync();
        var input = new PresenceInput(new DateTime(2026, 10, 25, 2, 0, 0, DateTimeKind.Utc), null, null, "Có mặt");
        Assert.Equal(
            403,
            (await Assert.ThrowsAsync<AppException>(() => service.Save(f.Teacher, f.Session, teacher.Id, true, input, default))).Status);
        Assert.Equal(
            403,
            (await Assert.ThrowsAsync<AppException>(() => service.Save(f.User, f.Session, Guid.NewGuid(), true, input, default))).Status);
        await service.Save(f.User, f.Session, teacher.Id, true, input, default);
        Assert.Equal(
            409,
            (await Assert.ThrowsAsync<AppException>(() => service.Save(f.User, f.Session, teacher.Id, true, input, default))).Status);
        var record = await f.Db.CatechistAttendance.SingleAsync();
        await service.Save(f.User, f.Session, teacher.Id, true, input with
        {
            Version = record.Version,
            CheckedOutAtUtc = input.CheckedInAtUtc.AddMinutes(5)
        }, default);
        Assert.Equal(2, await f.Db.Audit.CountAsync());
        Assert.Equal(
            400,
            (await Assert.ThrowsAsync<AppException>(() => service.Save(
            f.User,
            f.Session,
            teacher.Id,
            true,
            input with
            {
                CheckedOutAtUtc = input.CheckedInAtUtc.AddMinutes(-1)
            },
            default))).Status);
    }
    [Fact]
    public async Task StudentTimeCorrectionIsAuditedAndDoesNotChangePresenceCount()
    {
        await using var f = new Fixture();
        await f.Init();
        var scan = await f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "time-test-0001")), default);
        var service = new PresenceService(f.Db, f.Access, f.Clock);
        await service.Save(
            f.User,
            f.Session,
            f.Student,
            false,
            new(
            new DateTime(2026, 10, 25, 2, 1, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 25, 2, 9, 0, DateTimeKind.Utc),
            scan.Version,
            "Sửa giờ thực tế"),
            default);
        Assert.Single(await f.Db.Attendance.Where(x => x.Present).ToListAsync());
        Assert.NotNull((await f.Db.Attendance.SingleAsync()).CheckedOutAtUtc);
        Assert.Contains(await f.Db.Audit.ToListAsync(), x => x.Action == "AdjustTimes");
    }
}
