using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace CheckInApp.Tests;

public sealed class FixedClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 25, 2, 10, 0, TimeSpan.Zero);
}
public sealed class Fixture : IAsyncDisposable
{
    public SqliteConnection Connection { get; } = new("Data Source=:memory:"); public AppDbContext Db { get; private set; } = null!; public FixedClock Clock { get; } = new(); public Guid User { get; } = Guid.NewGuid(); public Guid Teacher { get; } = Guid.NewGuid(); public Guid Student { get; } = Guid.NewGuid(); public Guid Session { get; } = Guid.NewGuid(); public Guid ClassA { get; } = Guid.NewGuid(); public Guid ClassB { get; } = Guid.NewGuid(); public Guid Enrollment { get; } = Guid.NewGuid(); public AccessService Access => new(Db, Clock);
    public async Task Init()
    {
        await Connection.OpenAsync();
        Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options);
        await Db.Database.EnsureCreatedAsync();
        var year = new AcademicYear { Name = "2026–2027", StartDate = new(2026, 9, 1), EndDate = new(2027, 8, 31) };
        Db.Years.Add(year);
        Db.Classes.AddRange(new()
        {
            Id = ClassA,
            AcademicYearId = year.Id,
            Code = "A",
            Name = "A"
        }, new()
        {
            Id = ClassB,
            AcademicYearId = year.Id,
            Code = "B",
            Name = "B"
        });
        Db.Students.Add(new()
        {
            Id = Student,
            Code = "HS1",
            FullName = "Nguyễn Gia Bảo",
            DateOfBirth = new(2016, 1, 1)
        });
        Db.Users.AddRange(new()
        {
            Id = User,
            Email = "admin@test.local",
            FullName = "Admin",
            Status = AccountStatus.Active
        }, new()
        {
            Id = Teacher,
            Email = "teacher@test.local",
            FullName = "GLV",
            Status = AccountStatus.Active
        });
        var role = new AppRole { Name = "Admin", Permissions = CatalogService.AllPermissions, GlobalScope = true };
        var teacherRole = new AppRole { Name = "Teacher", Permissions = (long)(Permission.CheckIn | Permission.ReportView | Permission.AttendanceView) };
        Db.Roles.AddRange(role, teacherRole);
        Db.UserRoles.AddRange(new()
        {
            UserId = User,
            RoleId = role.Id
        }, new()
        {
            UserId = Teacher,
            RoleId = teacherRole.Id
        });
        var ca = new Catechist { FullName = "GLV", UserId = Teacher };
        Db.Catechists.Add(ca);
        Db.Assignments.Add(new()
        {
            ClassRoomId = ClassA,
            CatechistId = ca.Id,
            From = new(2026, 9, 1)
        });
        Db.Enrollments.Add(new()
        {
            Id = Enrollment,
            StudentId = Student,
            ClassRoomId = ClassA,
            From = new(2026, 9, 1)
        });
        Db.Sessions.Add(new()
        {
            Id = Session,
            ClassRoomId = ClassA,
            Date = new(2026, 10, 25),
            OpensAtUtc = new(2026, 10, 25, 2, 0, 0),
            ClosesAtUtc = new(2026, 10, 25, 2, 30, 0)
        });
        Db.Rosters.Add(new()
        {
            SessionId = Session,
            StudentId = Student,
            EnrollmentId = Enrollment
        });
        Db.Credentials.Add(new()
        {
            StudentId = Student,
            TokenHash = AuthService.Hash("CI1-valid-card"),
            IssuedAtUtc = Clock.GetUtcNow().UtcDateTime
        });
        await Db.SaveChangesAsync();
    }
    public AttendanceService Service => new(Db, Access, Clock, new RedisCache(new ConfigurationBuilder().Build()));
    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
public class AttendanceTests
{
    [Fact]
    public async Task RetryAndRepeatedScanNeverAddSecondRecord()
    {
        await using var f = new Fixture();
        await f.Init();
        var input = new CheckInInput("CI1-valid-card", "request-0001");
        var first = await f.Service.CheckIn(new(f.Teacher, f.Session, input), default);
        var retry = await f.Service.CheckIn(new(f.Teacher, f.Session, input), default);
        var duplicate = await f.Service.CheckIn(new(f.Teacher, f.Session, input with
        {
            ClientRequestId = "request-0002"
        }), default);
        Assert.Equal(first, retry);
        Assert.Equal("AlreadyCheckedIn", duplicate.ResultCode);
        Assert.Equal(1, await f.Db.Attendance.CountAsync());
    }
    [Fact]
    public async Task SameIdempotencyKeyWithOtherPayloadIsConflict()
    {
        await using var f = new Fixture();
        await f.Init();
        await f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "request-0001")), default);
        var e = await Assert.ThrowsAsync<AppException>(() => f.Service.CheckIn(new(f.User, f.Session, new("CI1-wrong-card", "request-0001")), default));
        Assert.Equal(409, e.Status);
    }
    [Fact]
    public async Task TeacherCannotScanOtherClass()
    {
        await using var f = new Fixture();
        await f.Init();
        var other = new AttendanceSession { ClassRoomId = f.ClassB, Date = new(2026, 10, 25), OpensAtUtc = new(2026, 10, 25, 2, 0, 0), ClosesAtUtc = new(2026, 10, 25, 2, 30, 0) };
        f.Db.Sessions.Add(other);
        await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AppException>(() => f.Service.CheckIn(new(f.Teacher, other.Id, new("CI1-valid-card", "request-0001")), default));
        Assert.Equal("CLASS_SCOPE", e.Code);
        Assert.Empty(f.Db.Attendance);
    }
    [Fact]
    public async Task PendingAccountCannotUseBusinessPermissions()
    {
        await using var f = new Fixture();
        await f.Init();
        (await f.Db.Users.FindAsync(f.Teacher))!.Status = AccountStatus.PendingApproval;
        await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AppException>(() => f.Access.Require(f.Teacher, Permission.CheckIn));
        Assert.Equal(403, e.Status);
    }
    [Fact]
    public async Task ClosedSessionCannotAcceptNewCheckIn()
    {
        await using var f = new Fixture();
        await f.Init();
        (await f.Db.Sessions.FindAsync(f.Session))!.ClosesAtUtc = new(2026, 10, 25, 2, 5, 0);
        await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AppException>(() => f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "request-0001")), default));
        Assert.Equal("SESSION_CLOSED", e.Code);
    }
    [Fact]
    public async Task RevokedCredentialIsRejected()
    {
        await using var f = new Fixture();
        await f.Init();
        (await f.Db.Credentials.SingleAsync()).RevokedAtUtc = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        var e = await Assert.ThrowsAsync<AppException>(() => f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "request-0001")), default));
        Assert.Equal("CREDENTIAL_INVALID", e.Code);
    }
    [Fact]
    public async Task ReportExcludesOpenFutureCancelledAndUnenrolledSessions()
    {
        await using var f = new Fixture();
        await f.Init();
        var closed = new AttendanceSession { ClassRoomId = f.ClassA, Date = new(2026, 10, 18), OpensAtUtc = new(2026, 10, 18, 2, 0, 0), ClosesAtUtc = new(2026, 10, 18, 2, 30, 0) };
        var cancelled = new AttendanceSession { ClassRoomId = f.ClassA, Date = new(2026, 10, 11), Cancelled = true, OpensAtUtc = new(2026, 10, 11, 2, 0, 0), ClosesAtUtc = new(2026, 10, 11, 2, 30, 0) };
        var future = new AttendanceSession { ClassRoomId = f.ClassA, Date = new(2026, 11, 1), OpensAtUtc = new(2026, 11, 1, 2, 0, 0), ClosesAtUtc = new(2026, 11, 1, 2, 30, 0) };
        f.Db.Sessions.AddRange(closed, cancelled, future);
        foreach (var ss in new[] { closed, cancelled, future })
            f.Db.Rosters.Add(new()
            {
                SessionId = ss.Id,
                StudentId = f.Student,
                EnrollmentId = f.Enrollment
            });
        f.Db.Attendance.Add(new()
        {
            SessionId = closed.Id,
            StudentId = f.Student,
            ActorId = f.User,
            Present = true
        });
        await f.Db.SaveChangesAsync();
        var report = await f.Service.Report(new(f.User, f.ClassA, new(2026, 9, 1), new(2026, 11, 30)), default);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(report);
        Assert.Equal(1, json.GetProperty("totalExpected").GetInt32());
        Assert.Equal(1, json.GetProperty("totalPresent").GetInt32());
    }
    [Fact]
    public async Task CorrectedAbsenceIsNotOverwrittenByNewScan()
    {
        await using var f = new Fixture();
        await f.Init();
        var first = await f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "request-0001")), default);
        var admin = new AdminService(f.Db, f.Access, f.Clock);
        await admin.Correct(f.User, first.AttendanceRecordId, false, first.Version, "Quét nhầm học sinh", default);
        var repeated = await f.Service.CheckIn(new(f.User, f.Session, new("CI1-valid-card", "request-0002")), default);
        Assert.False(repeated.Present);
        await Assert.ThrowsAsync<AppException>(() => admin.Correct(f.User, first.AttendanceRecordId, true, first.Version, "Phiên cũ", default));
    }
    [Fact]
    public async Task ScheduleExceptionAndReplayAreDeterministic()
    {
        await using var f = new Fixture();
        await f.Init();
        f.Db.Schedules.Add(new()
        {
            ClassRoomId = f.ClassB,
            From = new(2026, 10, 26),
            Until = new(2026, 10, 28),
            DaysMask = 127,
            OpensAt = new(9, 0),
            ClosesAt = new(9, 30)
        });
        f.Db.Exceptions.Add(new()
        {
            ClassRoomId = f.ClassB,
            Date = new(2026, 10, 27),
            Cancelled = true,
            Reason = "Nghỉ"
        });
        await f.Db.SaveChangesAsync();
        var schedules = new ScheduleService(f.Db, f.Clock);
        await schedules.Synchronize(f.ClassB);
        await schedules.Synchronize(f.ClassB);
        Assert.Equal(2, await f.Db.Sessions.CountAsync(x => x.ClassRoomId == f.ClassB && !x.Cancelled));
    }
    [Theory][InlineData(0, 0, null)][InlineData(2, 3, 66.67)][InlineData(3, 3, 100d)] public void RateHasExplicitZeroDenominator(int p, int e, double? expected) => Assert.Equal(expected == null ? (decimal?)null : (decimal)expected, AttendanceRules.Rate(p, e));
}
