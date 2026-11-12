using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public class ReadService(AppDbContext db, AccessService access, TimeProvider clock) : IReadService
{
    public static string[] SearchTerms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];
        if (value.Length > 120)
            throw new AppException(400, "SEARCH_TOO_LONG", "Từ khóa tối đa 120 ký tự.");
        var terms = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (terms.Length > 8)
            throw new AppException(400, "SEARCH_TOO_LONG", "Tìm tối đa 8 từ khóa.");
        return terms;
    }
    public async Task<object> List(
        Guid actor,
        string kind,
        string? q,
        Guid? classId,
        Guid? yearId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var rights = await access.Rights(actor, ct);
        void Require(Permission p)
        {
            if ((rights.Mask & (long)p) != (long)p)
                throw new AppException(403, "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.");
        }
        var terms = SearchTerms(q);
        page = Math.Clamp(page, 1, 100000);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        if (kind == "years")
        {
            Require(Permission.YearView);
            var yearsQuery = db.Years.AsNoTracking();
            foreach (var term in terms)
                yearsQuery = yearsQuery.Where(x => x.Name.Contains(term));
            return new
            {
                total = await yearsQuery.CountAsync(ct),
                items = await yearsQuery.OrderByDescending(x => x.StartDate).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct)
            };
        }
        var ids = await access.ClassIds(actor, ct, rights);
        if (classId != null)
        {
            if (!ids.Contains(classId.Value))
                throw new AppException(403, "CLASS_SCOPE", "Bạn không phụ trách lớp này.");
            ids = [classId.Value];
        }
        ids = await db.Classes
            .Where(x => ids.Contains(x.Id))
            .WhereIf(yearId != null, x => x.AcademicYearId == yearId)
            .Select(x => x.Id)
            .ToListAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        var skip = (page - 1) * pageSize;
        var today = AttendanceRules.Today(clock);
        switch (kind)
        {
            case "years":
                Require(Permission.YearView);
                return new
                {
                    items = await db.Years.AsNoTracking().OrderByDescending(x => x.StartDate).ToListAsync(ct)
                };
            case "classes":
                Require(Permission.ClassView);
                var cls = db.Classes.AsNoTracking()
                    .Where(x => ids.Contains(x.Id))
                    .WhereIf(yearId != null, x => x.AcademicYearId == yearId);
                foreach (var term in terms)
                    cls = cls.Where(x => x.Name.Contains(term) || x.Code.Contains(term));
                return new
                {
                    total = await cls.CountAsync(ct),
                    items = await cls.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(skip).Take(pageSize).Select(x => new { x.Id, x.Name, x.Code, x.AcademicYearId, studentCount = db.Enrollments.Count(e => e.ClassRoomId == x.Id && e.From <= today && (e.Until == null || today < e.Until)) })
                        .ToListAsync(ct)
                };
            case "students":
                Require(Permission.StudentView);
                var enroll = db.Enrollments.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId) && x.From <= today && (x.Until == null || today < x.Until));
                var studentIds = enroll.Select(x => x.StudentId);
                var st = db.Students.AsNoTracking().Where(x => studentIds.Contains(x.Id));
                foreach (var term in terms)
                {
                    var normalized = CatalogService.Search(term);
                    st = st.Where(x => x.SearchName.Contains(normalized) || x.Code.Contains(term));
                }
                return new
                {
                    total = await st.CountAsync(ct),
                    items = await st.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip(skip).Take(pageSize).Select(x => new
                    {
                        x.Id,
                        x.Code,
                        x.FamilyName,
                        x.GivenName,
                        x.FullName,
                        x.SaintName,
                        x.DateOfBirth,
                        hasAvatar = db.Audit.Where(a => a.EntityId == x.Id && a.EntityType == "students" && a.Action == "Avatar").OrderByDescending(a => a.CreatedAtUtc)
                        .ThenByDescending(a => a.Id)
                        .Select(a => a.Detail != "")
                        .FirstOrDefault(),
                        enrollments = enroll.Where(e => e.StudentId == x.Id).Select(e => new { e.Id, e.ClassRoomId, e.From, e.Until }).ToList()
                    })
                        .ToListAsync(ct)
                };
            case "catechists":
                Require(Permission.CatechistView);
                var cas = db.Catechists.AsNoTracking()
                    .WhereIf(!rights.Global, c => db.Assignments.Any(a => a.CatechistId == c.Id && ids.Contains(a.ClassRoomId)));
                foreach (var term in terms)
                    cas = cas.Where(x => x.FullName.Contains(term) || (x.SaintName != null && x.SaintName.Contains(term)));
                var totalTeachers = await cas.CountAsync(ct);
                var teacherPage = await cas.OrderBy(x => x.FullName).ThenBy(x => x.Id).Skip(skip).Take(pageSize).Select(x => new
                {
                    x.Id,
                    x.FamilyName,
                    x.GivenName,
                    x.FullName,
                    x.SaintName,
                    x.UserId,
                    hasAvatar = db.Audit.Where(a => a.EntityId == x.Id && a.EntityType == "catechists" && a.Action == "Avatar").OrderByDescending(a => a.CreatedAtUtc)
                    .ThenByDescending(a => a.Id)
                    .Select(a => a.Detail != "")
                    .FirstOrDefault()
                })
                    .ToListAsync(ct);
                var teacherIds = teacherPage.Select(x => x.Id).ToList();
                return new
                {
                    total = totalTeachers,
                    items = teacherPage,
                    assignments = await db.Assignments.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId) && teacherIds.Contains(x.CatechistId)).ToListAsync(ct)
                };
            case "roles":
                Require(Permission.RoleManage);
                return new
                {
                    items = (await db.Roles.AsNoTracking().ToListAsync(ct)).Select(r => new { r.Id, r.Name, permissions = r.Permissions.ToString(), r.GlobalScope }),
                    permissions = Enum.GetValues<Permission>().Where(p => p != Permission.None).Select(p => new { name = p.ToString(), value = ((long)p).ToString() })
                };
            case "users":
                Require(Permission.UserView);
                return new
                {
                    items = await db.Users.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100).Select(x => new
                    {
                        x.Id,
                        x.FullName,
                        x.Email,
                        status = x.Status.ToString(),
                        x.CreatedAtUtc,
                        x.ApprovedAtUtc,
                        catechistId = db.Catechists.Where(c => c.UserId == x.Id).Select(c => (Guid?)c.Id).FirstOrDefault(),
                        profileVersion = db.Audit.Where(a => a.EntityId == x.Id && a.Action == "EditProfile").OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
                        .Select(a => (Guid?)a.Id)
                        .FirstOrDefault()
                    })
                        .ToListAsync(ct)
                };
            case "schedules":
                Require(Permission.ScheduleView);
                return new
                {
                    items = await db.Schedules.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId)).ToListAsync(ct),
                    exceptions = await db.Exceptions.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId)).ToListAsync(ct)
                };
            case "audit":
                Require(Permission.AuditView);
                return new
                {
                    items = await db.Audit.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct)
                };
            default:
                throw new AppException(404, "NOT_FOUND", "Không có danh mục này.");
        }
    }
    public async Task<object> Sessions(Guid actor, DateOnly from, DateOnly until, Guid? classId, CancellationToken ct, Guid? yearId = null)
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
        ids = await db.Classes
            .Where(x => ids.Contains(x.Id))
            .WhereIf(yearId != null, x => x.AcademicYearId == yearId)
            .Select(x => x.Id)
            .ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await (from s in db.Sessions.AsNoTracking()
                          join c in db.Classes on s.ClassRoomId equals c.Id
                          where ids.Contains(c.Id) && s.Date >= @from && s.Date <= until
                          orderby s.Date, s.OpensAtUtc
                          select new
                          {
                              s.Id,
                              s.ClassRoomId,
                              className = c.Name,
                              classCode = c.Code,
                              s.ScheduleId,
                              teacherName = db.Catechists.Where(g => db.Assignments.Any(a => a.CatechistId == g.Id && a.ClassRoomId == c.Id && a.From <= s.Date && (a.Until == null || s.Date < a.Until)))
                          .OrderBy(g => g.FullName)
                          .Select(g => ((g.SaintName ?? "") + " " + g.FullName).Trim())
                          .FirstOrDefault(),
                              s.Date,
                              s.OpensAtUtc,
                              s.ClosesAtUtc,
                              s.StudentAttendanceEnabled,
                              s.CatechistAttendanceEnabled,
                              s.Cancelled,
                              s.Version,
                              expected = db.Rosters.Count(x => x.SessionId == s.Id),
                              present = db.Attendance.Count(x => x.SessionId == s.Id && x.Present)
                          }).ToListAsync(ct);
        return new
        {
            items = rows.Select(s => new
            {
                s.Id,
                s.ScheduleId,
                s.ClassRoomId,
                s.className,
                s.classCode,
                s.teacherName,
                s.Date,
                s.OpensAtUtc,
                s.ClosesAtUtc,
                s.StudentAttendanceEnabled,
                s.CatechistAttendanceEnabled,
                s.Cancelled,
                s.Version,
                s.expected,
                s.present,
                missing = s.expected - s.present,
                status = s.Cancelled
                ? "Cancelled"
                : now < s.OpensAtUtc ? "Scheduled" : now >= s.ClosesAtUtc ? "Closed" : "Open"
            })
        };
    }
    public async Task<object> Roster(Guid actor, Guid id, CancellationToken ct)
    {
        await access.Require(actor, Permission.AttendanceView, ct);
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "NOT_FOUND", "Không có buổi điểm danh.");
        await access.Class(actor, session.ClassRoomId, ct);
        var rows = await (from r in db.Rosters.AsNoTracking()
                          join s in db.Students on r.StudentId equals s.Id
                          join a in db.Attendance.Where(x => x.SessionId == id) on r.StudentId equals a.StudentId into records
                          from a in records.DefaultIfEmpty()
                          where r.SessionId == id
                          orderby s.FullName
                          select new
                          {
                              studentId = s.Id,
                              s.Code,
                              s.FullName,
                              s.SaintName,
                              s.DateOfBirth,
                              present = a != null && a.Present,
                              recordId = a == null ? (Guid?)null : a.Id,
                              checkedOutAtUtc = a == null ? (DateTime?)null : a.CheckedOutAtUtc,
                              recordedAtUtc = a == null ? (DateTime?)null : a.RecordedAtUtc,
                              version = a == null ? (Guid?)null : a.Version
                          }).ToListAsync(ct);
        return new
        {
            session,
            items = rows
        };
    }
}

