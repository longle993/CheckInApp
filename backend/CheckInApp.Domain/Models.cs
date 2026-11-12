namespace CheckInApp.Domain;

[Flags]
public enum Permission : long
{
    None = 0, ClassView = 1L << 0, ClassManage = 1L << 1, StudentView = 1L << 2, StudentManage = 1L << 3,
    CatechistView = 1L << 4, CatechistManage = 1L << 5, YearView = 1L << 6, YearManage = 1L << 7,
    ScheduleView = 1L << 8, ScheduleManage = 1L << 9, AttendanceView = 1L << 10, CheckIn = 1L << 11,
    AttendanceCorrect = 1L << 12, ReportView = 1L << 13, UserView = 1L << 14, UserApprove = 1L << 15,
    RoleManage = 1L << 16, CredentialManage = 1L << 17, AssignmentManage = 1L << 18, AuditView = 1L << 19, UserCreate = 1L << 20,
    StudentImport = 1L << 21, StudentExport = 1L << 22, CatechistImport = 1L << 23, CatechistExport = 1L << 24,
    ClassImport = 1L << 25, ClassExport = 1L << 26, CatechistAttendance = 1L << 27,
    WebSettingsView = 1L << 28, WebSettingsManage = 1L << 29, UserManage = 1L << 30,
    ClassDelete = 1L << 31, StudentDelete = 1L << 32, CatechistDelete = 1L << 33, YearDelete = 1L << 34, YearImport = 1L << 35, YearExport = 1L << 36
}
public enum AccountStatus
{
    PendingApproval, Active, Rejected, Suspended
}
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
public class AcademicYear : Entity
{
    public string Name { get; set; } = ""; public DateOnly StartDate
    {
        get; set;
    }
    public DateOnly EndDate
    {
        get; set;
    }
    public bool IsActive
    {
        get; set;
    }
}
public class ClassRoom : Entity
{
    public Guid AcademicYearId
    {
        get; set;
    }
    public string Code { get; set; } = ""; public string Name { get; set; } = "";
}
public class Student : Entity
{
    public string FamilyName { get; set; } = "";
    public string GivenName { get; set; } = "";
    public string Code { get; set; } = ""; public string? SaintName
    {
        get; set;
    }
    public string FullName { get; set; } = ""; public DateOnly DateOfBirth
    {
        get; set;
    }
    public string SearchName { get; set; } = "";
}
public class Enrollment : Entity
{
    public Guid StudentId
    {
        get; set;
    }
    public Guid ClassRoomId
    {
        get; set;
    }
    public DateOnly From
    {
        get; set;
    }
    public DateOnly? Until
    {
        get; set;
    }
}
public class Catechist : Entity
{
    public string FamilyName { get; set; } = "";
    public string GivenName { get; set; } = "";
    public string FullName { get; set; } = ""; public string? SaintName
    {
        get; set;
    }
    public Guid? UserId
    {
        get; set;
    }
}
public class ClassAssignment : Entity
{
    public Guid ClassRoomId
    {
        get; set;
    }
    public Guid CatechistId
    {
        get; set;
    }
    public DateOnly From
    {
        get; set;
    }
    public DateOnly? Until
    {
        get; set;
    }
}
public class AttendanceSchedule : Entity
{
    public bool StudentAttendanceEnabled { get; set; } = true;
    public bool CatechistAttendanceEnabled { get; set; } = true;
    public Guid ClassRoomId
    {
        get; set;
    }
    public int DaysMask
    {
        get; set;
    }
    public DateOnly From
    {
        get; set;
    }
    public DateOnly Until
    {
        get; set;
    }
    public TimeOnly OpensAt
    {
        get; set;
    }
    public TimeOnly ClosesAt
    {
        get; set;
    }
    public int Version { get; set; } = 1;
}
public class ScheduleException : Entity
{
    public Guid? ScheduleId
    {
        get; set;
    }
    public DateOnly? MovedToDate
    {
        get; set;
    }
    public bool StudentAttendanceEnabled { get; set; } = true;
    public bool CatechistAttendanceEnabled { get; set; } = true;
    public Guid ClassRoomId
    {
        get; set;
    }
    public DateOnly Date
    {
        get; set;
    }
    public bool Cancelled
    {
        get; set;
    }
    public TimeOnly OpensAt
    {
        get; set;
    }
    public TimeOnly ClosesAt
    {
        get; set;
    }
    public string Reason { get; set; } = "";
}
public class AttendanceSession : Entity
{
    public Guid? ScheduleId
    {
        get; set;
    }
    public DateOnly? SourceDate
    {
        get; set;
    }
    public bool StudentAttendanceEnabled { get; set; } = true;
    public bool CatechistAttendanceEnabled { get; set; } = true;
    public Guid ClassRoomId
    {
        get; set;
    }
    public DateOnly Date
    {
        get; set;
    }
    public DateTime OpensAtUtc
    {
        get; set;
    }
    public DateTime ClosesAtUtc
    {
        get; set;
    }
    public bool Cancelled
    {
        get; set;
    }
    public int ScheduleVersion
    {
        get; set;
    }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public class SessionRoster : Entity
{
    public Guid SessionId
    {
        get; set;
    }
    public Guid StudentId
    {
        get; set;
    }
    public Guid EnrollmentId
    {
        get; set;
    }
}
public class StudentCredential : Entity
{
    public Guid StudentId
    {
        get; set;
    }
    public string TokenHash { get; set; } = ""; public DateTime IssuedAtUtc
    {
        get; set;
    }
    public DateTime? RevokedAtUtc
    {
        get; set;
    }
}
public class CatechistCredential : Entity
{
    public Guid CatechistId
    {
        get; set;
    }
    public string TokenHash { get; set; } = "";
    public DateTime IssuedAtUtc
    {
        get; set;
    }
    public DateTime? RevokedAtUtc
    {
        get; set;
    }
}
public class AttendanceRecord : Entity
{
    public DateTime? CheckedOutAtUtc
    {
        get; set;
    }
    public Guid SessionId
    {
        get; set;
    }
    public Guid StudentId
    {
        get; set;
    }
    public bool Present { get; set; } = true; public Guid ActorId
    {
        get; set;
    }
    public DateTime RecordedAtUtc
    {
        get; set;
    }
    public string Method { get; set; } = "QR"; public Guid Version { get; set; } = Guid.NewGuid();
}
public class CatechistAttendanceRecord : Entity
{
    public Guid SessionId
    {
        get; set;
    }
    public Guid CatechistId
    {
        get; set;
    }
    public DateTime CheckedInAtUtc
    {
        get; set;
    }
    public DateTime? CheckedOutAtUtc
    {
        get; set;
    }
    public Guid ActorId
    {
        get; set;
    }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public class IdempotencyRequest : Entity
{
    public Guid ActorId
    {
        get; set;
    }
    public string Key { get; set; } = ""; public string PayloadHash { get; set; } = ""; public Guid AttendanceRecordId
    {
        get; set;
    }
    public string ResponseJson { get; set; } = "";
}
public class AuditLog : Entity
{
    public Guid ActorId
    {
        get; set;
    }
    public string Action { get; set; } = ""; public string EntityType { get; set; } = ""; public Guid EntityId
    {
        get; set;
    }
    public string Detail { get; set; } = ""; public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public class AppUser : Entity
{
    public string Email { get; set; } = ""; public string FullName { get; set; } = ""; public string PasswordHash { get; set; } = ""; public AccountStatus Status
    {
        get; set;
    }
    public Guid? ApprovedBy
    {
        get; set;
    }
    public DateTime? ApprovedAtUtc
    {
        get; set;
    }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
public class AppRole : Entity
{
    public string Name { get; set; } = ""; public long Permissions
    {
        get; set;
    }
    public bool GlobalScope
    {
        get; set;
    }
}
public class UserRole : Entity
{
    public Guid UserId
    {
        get; set;
    }
    public Guid RoleId
    {
        get; set;
    }
}
public class AuthSession : Entity
{
    public Guid UserId
    {
        get; set;
    }
    public DateTime ExpiresAtUtc
    {
        get; set;
    }
    public bool Revoked
    {
        get; set;
    }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public class RefreshToken : Entity
{
    public Guid AuthSessionId
    {
        get; set;
    }
    public string Hash { get; set; } = ""; public bool Used
    {
        get; set;
    }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public static class AttendanceRules
{
    public static bool Eligible(DateOnly date, DateOnly from, DateOnly? until) => date >= from && (until == null || date < until);
    public static bool Scheduled(DateOnly date, int daysMask) => (daysMask & (1 << (int)date.DayOfWeek)) != 0;
    public static decimal? Rate(int present, int expected) => expected == 0 ? null : Math.Round(present * 100m / expected, 2);
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));
    public static DateTime ToUtc(DateOnly date, TimeOnly time) => DateTime.SpecifyKind(date.ToDateTime(time).AddHours(-7), DateTimeKind.Utc);
}
