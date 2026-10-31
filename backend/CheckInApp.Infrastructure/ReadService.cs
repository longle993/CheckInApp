using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public class ReadService(AppDbContext db, AccessService access, TimeProvider clock)
{
    public async Task<object> List(Guid actor, string kind, string? q, Guid? classId, Guid? yearId, int page, int pageSize, CancellationToken ct)
    {
        var ids = await access.ClassIds(actor, ct);
        if (classId != null)
        {
            await access.Class(actor, classId.Value, ct);
            ids = [classId.Value];
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        var skip = (page - 1) * pageSize;
        var today = AttendanceRules.Today(clock);
        switch (kind)
        {
            case "years":
                await access.Require(actor, Permission.YearView, ct);
                return new
                {
                    items = await db.Years.AsNoTracking().OrderByDescending(x => x.StartDate).ToListAsync(ct)
                };
            case "classes":
                await access.Require(actor, Permission.ClassView, ct);
                var cls = db.Classes.AsNoTracking().Where(x => ids.Contains(x.Id));
                if (yearId != null)
                    cls = cls.Where(x => x.AcademicYearId == yearId);
                if (!string.IsNullOrWhiteSpace(q))
                    cls = cls.Where(x => x.Name.Contains(q) || x.Code.Contains(q));
                return new
                {
                    total = await cls.CountAsync(ct),
                    items = await cls.OrderBy(x => x.Name).Skip(skip).Take(pageSize).Select(x => new { x.Id, x.Name, x.Code, x.AcademicYearId, studentCount = db.Enrollments.Count(e => e.ClassRoomId == x.Id && e.From <= today && (e.Until == null || today < e.Until)) }).ToListAsync(ct)
                };
            case "students":
                await access.Require(actor, Permission.StudentView, ct);
                var enroll = db.Enrollments.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId) && x.From <= today && (x.Until == null || today < x.Until));
                var studentIds = enroll.Select(x => x.StudentId);
                var st = db.Students.AsNoTracking().Where(x => studentIds.Contains(x.Id));
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var normalized = CatalogService.Search(q);
                    st = st.Where(x => x.SearchName.Contains(normalized) || x.Code.Contains(q));
                }
                return new
                {
                    total = await st.CountAsync(ct),
                    items = await st.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip(skip).Take(pageSize).Select(x => new { x.Id, x.Code, x.FamilyName, x.GivenName, x.FullName, x.SaintName, x.DateOfBirth, enrollments = enroll.Where(e => e.StudentId == x.Id).Select(e => new { e.Id, e.ClassRoomId, e.From, e.Until }).ToList() }).ToListAsync(ct)
                };
            case "catechists":
                await access.Require(actor, Permission.CatechistView, ct);
                var cas = (await access.Rights(actor, ct)).Global ? db.Catechists.AsNoTracking() : db.Catechists.AsNoTracking().Where(c => db.Assignments.Any(a => a.CatechistId == c.Id && ids.Contains(a.ClassRoomId)));
                return new
                {
                    total = await cas.Where(x => q == null || x.FullName.Contains(q)).CountAsync(ct),
                    items = await cas.Where(x => q == null || x.FullName.Contains(q)).OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip(skip).Take(pageSize).ToListAsync(ct),
                    assignments = await db.Assignments.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId)).ToListAsync(ct)
                };
            case "roles":
                await access.Require(actor, Permission.RoleManage, ct);
                return new
                {
                    items = (await db.Roles.AsNoTracking().ToListAsync(ct)).Select(r => new { r.Id, r.Name, permissions = r.Permissions.ToString(), r.GlobalScope }),
                    permissions = Enum.GetValues<Permission>().Where(p => p != Permission.None).Select(p => new { name = p.ToString(), value = ((long)p).ToString() })
                };
            case "users":
                await access.Require(actor, Permission.UserView, ct);
                return new
                {
                    items = await db.Users.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100).Select(x => new { x.Id, x.FullName, x.Email, status = x.Status.ToString(), x.CreatedAtUtc, x.ApprovedAtUtc }).ToListAsync(ct)
                };
            case "schedules":
                await access.Require(actor, Permission.ScheduleView, ct);
                return new
                {
                    items = await db.Schedules.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId)).ToListAsync(ct),
                    exceptions = await db.Exceptions.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId)).ToListAsync(ct)
                };
            case "audit":
                await access.Require(actor, Permission.AuditView, ct);
                return new
                {
                    items = await db.Audit.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct)
                };
            default:
                throw new AppException(404, "NOT_FOUND", "Không có danh mục này.");
        }
    }
    public async Task<object> Sessions(Guid actor, DateOnly from, DateOnly until, Guid? classId, CancellationToken ct)
    {
        await access.Require(actor, Permission.AttendanceView, ct);
        if (until < from || until.DayNumber - from.DayNumber > 400)
            throw new AppException(400, "DATE_RANGE", "Khoảng lịch không hợp lệ.");
        var ids = await access.ClassIds(actor, ct);
        if (classId != null)
        {
            await access.Class(actor, classId.Value, ct);
            ids = [classId.Value];
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await (from s in db.Sessions.AsNoTracking() join c in db.Classes on s.ClassRoomId equals c.Id where ids.Contains(c.Id) && s.Date >= @from && s.Date <= until orderby s.Date, s.OpensAtUtc select new { s.Id, s.ClassRoomId, className = c.Name, classCode = c.Code, teacherName = db.Catechists.Where(g => db.Assignments.Any(a => a.CatechistId == g.Id && a.ClassRoomId == c.Id && a.From <= s.Date && (a.Until == null || s.Date < a.Until))).OrderBy(g => g.FullName).Select(g => g.FullName).FirstOrDefault(), s.Date, s.OpensAtUtc, s.ClosesAtUtc, s.StudentAttendanceEnabled, s.CatechistAttendanceEnabled, s.Cancelled, s.Version, expected = db.Rosters.Count(x => x.SessionId == s.Id), present = db.Attendance.Count(x => x.SessionId == s.Id && x.Present) }).ToListAsync(ct);
        return new
        {
            items = rows.Select(s => new { s.Id, s.ClassRoomId, s.className, s.classCode, s.teacherName, s.Date, s.OpensAtUtc, s.ClosesAtUtc, s.StudentAttendanceEnabled, s.CatechistAttendanceEnabled, s.Cancelled, s.Version, s.expected, s.present, missing = s.expected - s.present, status = s.Cancelled ? "Cancelled" : now < s.OpensAtUtc ? "Scheduled" : now >= s.ClosesAtUtc ? "Closed" : "Open" })
        };
    }
    public async Task<object> Roster(Guid actor, Guid id, CancellationToken ct)
    {
        await access.Require(actor, Permission.AttendanceView, ct);
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "NOT_FOUND", "Không có buổi điểm danh.");
        await access.Class(actor, session.ClassRoomId, ct);
        var rows = await (from r in db.Rosters.AsNoTracking() join s in db.Students on r.StudentId equals s.Id join a in db.Attendance.Where(x => x.SessionId == id) on r.StudentId equals a.StudentId into records from a in records.DefaultIfEmpty() where r.SessionId == id orderby s.FullName select new { studentId = s.Id, s.Code, s.FullName, s.SaintName, s.DateOfBirth, present = a != null && a.Present, recordId = a == null ? (Guid?)null : a.Id, checkedOutAtUtc = a == null ? (DateTime?)null : a.CheckedOutAtUtc, recordedAtUtc = a == null ? (DateTime?)null : a.RecordedAtUtc, version = a == null ? (Guid?)null : a.Version }).ToListAsync(ct);
        return new
        {
            session,
            items = rows
        };
    }
}

