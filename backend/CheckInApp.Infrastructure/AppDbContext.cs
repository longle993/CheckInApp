using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Student>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var student = entry.Entity;
            if ((string.IsNullOrWhiteSpace(student.FamilyName) && string.IsNullOrWhiteSpace(student.GivenName)) ||
                (entry.State == EntityState.Modified && entry.Property(x => x.FullName).IsModified && !entry.Property(x => x.FamilyName).IsModified && !entry.Property(x => x.GivenName).IsModified))
                (student.FamilyName,student.GivenName) = StudentNames.Split(student.FullName);
            student.FamilyName=StudentNames.Clean(student.FamilyName);student.GivenName=StudentNames.Clean(student.GivenName);
            student.FullName=StudentNames.Full(student.FamilyName,student.GivenName);
            student.SearchName=CatalogService.Search(student.FullName+" "+student.SaintName);
        }
        foreach (var entry in ChangeTracker.Entries<Catechist>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var person=entry.Entity;
            if ((string.IsNullOrWhiteSpace(person.FamilyName) && string.IsNullOrWhiteSpace(person.GivenName)) ||
                (entry.State==EntityState.Modified && entry.Property(x=>x.FullName).IsModified && !entry.Property(x=>x.FamilyName).IsModified && !entry.Property(x=>x.GivenName).IsModified))
                (person.FamilyName,person.GivenName)=StudentNames.Split(person.FullName);
            person.FamilyName=StudentNames.Clean(person.FamilyName);person.GivenName=StudentNames.Clean(person.GivenName);
            person.FullName=StudentNames.Full(person.FamilyName,person.GivenName);
        }
        return base.SaveChangesAsync(cancellationToken);
    }
    public DbSet<CatechistAttendanceRecord> CatechistAttendance => Set<CatechistAttendanceRecord>();
    public DbSet<CatechistCredential> CatechistCredentials => Set<CatechistCredential>();
    public DbSet<AcademicYear> Years => Set<AcademicYear>(); public DbSet<ClassRoom> Classes => Set<ClassRoom>(); public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>(); public DbSet<Catechist> Catechists => Set<Catechist>(); public DbSet<ClassAssignment> Assignments => Set<ClassAssignment>();
    public DbSet<AttendanceSchedule> Schedules => Set<AttendanceSchedule>(); public DbSet<ScheduleException> Exceptions => Set<ScheduleException>(); public DbSet<AttendanceSession> Sessions => Set<AttendanceSession>();
    public DbSet<SessionRoster> Rosters => Set<SessionRoster>(); public DbSet<StudentCredential> Credentials => Set<StudentCredential>(); public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();
    public DbSet<IdempotencyRequest> Idempotency => Set<IdempotencyRequest>(); public DbSet<AuditLog> Audit => Set<AuditLog>(); public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>(); public DbSet<UserRole> UserRoles => Set<UserRole>(); public DbSet<AuthSession> AuthSessions => Set<AuthSession>(); public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<CatechistCredential>().HasIndex(x => x.TokenHash).IsUnique();
        m.Entity<CatechistCredential>().Property(x => x.TokenHash).HasMaxLength(64);
        m.Entity<CatechistCredential>().HasOne<Catechist>().WithMany().HasForeignKey(x => x.CatechistId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AttendanceSchedule>().Property(x => x.StudentAttendanceEnabled).HasDefaultValue(true);
        m.Entity<AttendanceSchedule>().Property(x => x.CatechistAttendanceEnabled).HasDefaultValue(true);
        m.Entity<AttendanceSession>().Property(x => x.StudentAttendanceEnabled).HasDefaultValue(true);
        m.Entity<AttendanceSession>().Property(x => x.CatechistAttendanceEnabled).HasDefaultValue(true);
        m.Entity<ScheduleException>().Property(x => x.StudentAttendanceEnabled).HasDefaultValue(true);
        m.Entity<ScheduleException>().Property(x => x.CatechistAttendanceEnabled).HasDefaultValue(true);
        m.Entity<CatechistAttendanceRecord>().HasIndex(x => new { x.SessionId, x.CatechistId }).IsUnique();
        m.Entity<CatechistAttendanceRecord>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<CatechistAttendanceRecord>().HasOne<AttendanceSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CatechistAttendanceRecord>().HasOne<Catechist>().WithMany().HasForeignKey(x => x.CatechistId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AppUser>().ToTable("AspNetUsers").HasIndex(x => x.Email).IsUnique();
        m.Entity<AppRole>().ToTable("AspNetRoles").HasIndex(x => x.Name).IsUnique();
        m.Entity<UserRole>().ToTable("AspNetUserRoles").HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
        m.Entity<UserRole>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<UserRole>().HasOne<AppRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AcademicYear>().HasIndex(x => x.Name).IsUnique();
        m.Entity<ClassRoom>().HasIndex(x => new { x.AcademicYearId, x.Code }).IsUnique();
        m.Entity<Student>().HasIndex(x => x.Code).IsUnique();
        m.Entity<AttendanceSession>().HasIndex(x => new { x.ClassRoomId, x.Date }).IsUnique();
        m.Entity<SessionRoster>().HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
        m.Entity<AttendanceRecord>().HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
        m.Entity<IdempotencyRequest>().HasIndex(x => new { x.ActorId, x.Key }).IsUnique();
        m.Entity<StudentCredential>().HasIndex(x => x.TokenHash).IsUnique();
        m.Entity<RefreshToken>().HasIndex(x => x.Hash).IsUnique();
        m.Entity<ScheduleException>().HasIndex(x => new { x.ClassRoomId, x.Date }).IsUnique();
        m.Entity<AttendanceSchedule>().HasIndex(x => x.ClassRoomId).IsUnique();
        m.Entity<Catechist>().HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        m.Entity<AttendanceRecord>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<AttendanceSession>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<AuthSession>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<RefreshToken>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<ClassRoom>().HasOne<AcademicYear>().WithMany().HasForeignKey(x => x.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Enrollment>().HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Enrollment>().HasOne<ClassRoom>().WithMany().HasForeignKey(x => x.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClassAssignment>().HasOne<ClassRoom>().WithMany().HasForeignKey(x => x.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ClassAssignment>().HasOne<Catechist>().WithMany().HasForeignKey(x => x.CatechistId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AttendanceSchedule>().HasOne<ClassRoom>().WithMany().HasForeignKey(x => x.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ScheduleException>().HasOne<ClassRoom>().WithMany().HasForeignKey(x => x.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AttendanceSession>().HasOne<ClassRoom>().WithMany().HasForeignKey(x => x.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SessionRoster>().HasOne<AttendanceSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SessionRoster>().HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<SessionRoster>().HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AttendanceRecord>().HasOne<SessionRoster>().WithMany().HasForeignKey(x => new { x.SessionId, x.StudentId }).HasPrincipalKey(x => new { x.SessionId, x.StudentId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<StudentCredential>().HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Catechist>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<AuthSession>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<RefreshToken>().HasOne<AuthSession>().WithMany().HasForeignKey(x => x.AuthSessionId).OnDelete(DeleteBehavior.Restrict);
        foreach (var type in m.Model.GetEntityTypes())
        foreach (var p in type.GetProperties().Where(p => p.ClrType == typeof(string)))
            p.SetMaxLength(p.Name == "Detail" ? 4000 : 500);
        m.Entity<IdempotencyRequest>().Property(x => x.ResponseJson).HasMaxLength(4000);
        m.Entity<AppUser>().Property(x => x.Email).HasMaxLength(256);
        m.Entity<AppRole>().Property(x => x.Name).HasMaxLength(100);
        m.Entity<AcademicYear>().Property(x => x.Name).HasMaxLength(100);
        m.Entity<Student>().Property(x => x.Code).HasMaxLength(50);
        m.Entity<ClassRoom>().Property(x => x.Code).HasMaxLength(50);
        m.Entity<RefreshToken>().Property(x => x.Hash).HasMaxLength(64);
        m.Entity<StudentCredential>().Property(x => x.TokenHash).HasMaxLength(64);
        m.Entity<IdempotencyRequest>().Property(x => x.Key).HasMaxLength(100);
    }
}
