using System.Security.Cryptography;
using System.Text.Json;
using CheckInApp.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace CheckInApp.Infrastructure;

public static class Bootstrap
{
    public static async Task Seed(AppDbContext db, IConfiguration config, bool development, string root, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            var existingAdmin = await db.Roles.SingleOrDefaultAsync(x => x.Name == "Admin", ct);
            if (existingAdmin != null) existingAdmin.Permissions = CatalogService.AllPermissions;
            await db.SaveChangesAsync(ct);
            return;
        }
        var email = config["Bootstrap:AdminEmail"];
        var password = config["Bootstrap:AdminPassword"];
        if (!development && (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)))
            throw new InvalidOperationException("Set Bootstrap__AdminEmail and Bootstrap__AdminPassword for the first deployment.");
        email ??= "admin@checkin.local";
        password ??= "Ci!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        AuthService.ValidatePassword(password);
        var admin = new AppUser { Email = email.ToLowerInvariant(), FullName = "Quản trị viên", Status = AccountStatus.Active };
        admin.PasswordHash = new PasswordHasher<AppUser>().HashPassword(admin, password);
        var role = new AppRole { Name = "Admin", Permissions = CatalogService.AllPermissions, GlobalScope = true };
        var teacher = new AppRole { Name = "Giáo lý viên", Permissions = (long)(Permission.ClassView | Permission.StudentView | Permission.CatechistView | Permission.YearView | Permission.ScheduleView | Permission.AttendanceView | Permission.CheckIn | Permission.ReportView) };
        db.Users.Add(admin);
        db.Roles.AddRange(role, teacher);
        db.UserRoles.Add(new()
        {
            UserId = admin.Id,
            RoleId = role.Id
        });
        string? demoCredential = null;
        if (development && config.GetValue<bool>("SeedDemoData"))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var start = new DateOnly(today.Month >= 9 ? today.Year : today.Year - 1, 9, 1);
            var year = new AcademicYear { Name = $"{start.Year}–{start.Year + 1}", StartDate = start, EndDate = start.AddYears(1).AddDays(-1), IsActive = true };
            db.Years.Add(year);
            var names = new[] { "Nguyễn Gia Bảo", "Trần Ngọc Ánh", "Lê Hoàng Bảo Ngọc", "Nguyễn Phạm Hoàng Thiên Ân", "Đặng Minh Khang", "Võ Nguyễn Khánh Linh", "Nguyễn Gia Bảo", "Phan Đức Huy" };
            var classNames = new[] { "Khai Tâm 1", "Khai Tâm 2", "Xưng Tội 1", "Thêm Sức 2" };
            var catechistNames = new[] { "Maria Nguyễn Minh An", "Giuse Trần Minh Đức", "Anna Lê Thanh Hương", "Phêrô Nguyễn Hoàng Nam" };
            for (var i = 0; i < 4; i++)
            {
                var cls = new ClassRoom { AcademicYearId = year.Id, Code = $"L0{i + 1}", Name = classNames[i] };
                db.Classes.Add(cls);
                var catechist = new Catechist { FullName = catechistNames[i] };
                db.Catechists.Add(catechist);
                db.Assignments.Add(new()
                {
                    ClassRoomId = cls.Id,
                    CatechistId = catechist.Id,
                    From = start
                });
                db.Schedules.Add(new()
                {
                    ClassRoomId = cls.Id,
                    DaysMask = 1,
                    From = start,
                    Until = year.EndDate,
                    OpensAt = new(9, 0),
                    ClosesAt = new(9, 30)
                });
                for (var j = 0; j < 30; j++)
                {
                    var student = new Student { Code = $"HS{i + 1}{j + 1:000}", FullName = names[j % 8] + (j >= 8 ? $" {j + 1}" : ""), SaintName = j % 5 == 0 ? null : j % 2 == 0 ? "Giuse" : "Maria", DateOfBirth = new(2016, 1 + j % 12, 1 + j % 27) };
                    student.SearchName = CatalogService.Search(student.FullName + " " + student.SaintName);
                    db.Students.Add(student);
                    db.Enrollments.Add(new()
                    {
                        StudentId = student.Id,
                        ClassRoomId = cls.Id,
                        From = start
                    });
                    var token = "CI1-" + AuthService.RandomToken();
                    db.Credentials.Add(new()
                    {
                        StudentId = student.Id,
                        TokenHash = AuthService.Hash(token),
                        IssuedAtUtc = DateTime.UtcNow
                    });
                    if (i == 0 && j == 0)
                        demoCredential = token;
                }
            }
        }
        await db.SaveChangesAsync(ct);
        if (development)
        {
            var generator = new ScheduleService(db, TimeProvider.System);
            foreach (var id in await db.Classes.Select(x => x.Id).ToListAsync(ct))
                await generator.Synchronize(id, ct);
            // Explicit development-only extra sessions allow camera testing on a weekday as well.
            if (config.GetValue<bool>("SeedDemoData"))
            foreach (var cls in await db.Classes.ToListAsync(ct))
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
                var session = await db.Sessions.SingleOrDefaultAsync(x => x.ClassRoomId == cls.Id && x.Date == today, ct);
                if (session == null)
                {
                    session = new()
                    {
                        ClassRoomId = cls.Id,
                        Date = today
                    };
                    db.Sessions.Add(session);
                }
                session.OpensAtUtc = DateTime.UtcNow.AddMinutes(-30);
                session.ClosesAtUtc = DateTime.UtcNow.AddHours(2);
                var roster = await db.Rosters.Where(x => x.SessionId == session.Id).Select(x => x.StudentId).ToListAsync(ct);
                foreach (var e in await db.Enrollments.Where(x => x.ClassRoomId == cls.Id).ToListAsync(ct))
                if (!roster.Contains(e.StudentId))
                    db.Rosters.Add(new()
                    {
                        SessionId = session.Id,
                        StudentId = e.StudentId,
                        EnrollmentId = e.Id
                    });
            }
            await db.SaveChangesAsync(ct);
            Directory.CreateDirectory(Path.Combine(root, ".local"));
            await File.WriteAllTextAsync(Path.Combine(root, ".local", "bootstrap-admin.json"), JsonSerializer.Serialize(new
            {
                email,
                password,
                demoCredential,
                note = "Generated development credentials. Never deploy this file."
            }, new JsonSerializerOptions { WriteIndented = true }), ct);
        }
    }
}
