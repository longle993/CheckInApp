using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheckInApp.Infrastructure;

public class ReportingService(AppDbContext db, AccessService access, TimeProvider clock) : IReportingService
{
    public async Task<object> AttendanceCalendar(
        Guid actorId,
        Guid yearId,
        string kind,
        Guid personId,
        int year,
        int month,
        Guid? classId,
        CancellationToken ct)
    {
        await access.Require(actorId, Permission.AttendanceView, ct);
        if (kind is not ("students" or "catechists") || year < 1 || year > 9998 || month < 1 || month > 12)
            throw new AppException(400, "CALENDAR_INVALID", "Tháng, năm hoặc đối tượng không hợp lệ.");
        await access.Require(actorId, kind == "students" ? Permission.StudentView : Permission.CatechistView, ct);
        var allowed = await access.ClassIds(actorId, ct);
        if (classId != null)
            await access.Class(actorId, classId.Value, ct);
        var classes = await db.Classes
            .Where(x => allowed.Contains(x.Id) && x.AcademicYearId == yearId)
            .WhereIf(classId != null, x => x.Id == classId)
            .ToListAsync(ct);
        var classIds = classes.Select(x => x.Id).ToList();
        var from = new DateOnly(year, month, 1);
        var until = from.AddMonths(1);
        var sessions = await db.Sessions.AsNoTracking()
            .Where(x => classIds.Contains(x.ClassRoomId) && x.Date >= from && x.Date < until)
            .OrderBy(x => x.OpensAtUtc)
            .ToListAsync(ct);
        var sessionIds = sessions.Select(x => x.Id).ToList();
        var now = clock.GetUtcNow().UtcDateTime;
        var items = new List<object>();
        var rosters = await db.Rosters.Where(x => x.StudentId == personId && sessionIds.Contains(x.SessionId)).Select(x => x.SessionId).ToListAsync(ct);
        var enrollments = await db.Enrollments.Where(x => x.StudentId == personId && classIds.Contains(x.ClassRoomId)).ToListAsync(ct);
        var assignments = await db.Assignments.Where(x => x.CatechistId == personId && classIds.Contains(x.ClassRoomId)).ToListAsync(ct);
        if (kind == "students" ? enrollments.Count == 0 && rosters.Count == 0 : assignments.Count == 0)
            throw new AppException(403, "PERSON_SCOPE", "Người được chọn không thuộc phạm vi xem.");
        var records = await db.Attendance.Where(x => x.StudentId == personId && sessionIds.Contains(x.SessionId) && x.Present).ToListAsync(ct);
        var teacherRecords = await db.CatechistAttendance.Where(x => x.CatechistId == personId && sessionIds.Contains(x.SessionId)).ToListAsync(ct);
        foreach (var session in sessions)
        {
            var eligible = kind == "students"
                ? session.StudentAttendanceEnabled
                    && (rosters.Contains(session.Id)
                        || session.Cancelled
                        && enrollments.Any(x => x.ClassRoomId == session.ClassRoomId && AttendanceRules.Eligible(session.Date, x.From, x.Until)))
                : session.CatechistAttendanceEnabled
                    && assignments.Any(x => x.ClassRoomId == session.ClassRoomId && AttendanceRules.Eligible(session.Date, x.From, x.Until));
            if (!eligible)
                continue;
            DateTime? entered = kind == "students"
                ? records.FirstOrDefault(x => x.SessionId == session.Id)?.RecordedAtUtc
                : teacherRecords.FirstOrDefault(x => x.SessionId == session.Id)?.CheckedInAtUtc;
            var status = session.Cancelled
                ? "cancelled"
                : entered == DateTime.MinValue ? "unknown"
                : entered != null ? (entered > session.OpensAtUtc ? "late" : "ontime")
                : now < session.OpensAtUtc ? "future"
                : now >= session.ClosesAtUtc ? "absent"
                : "pending";
            items.Add(new
            {
                session.Id,
                session.Date,
                session.OpensAtUtc,
                session.ClosesAtUtc,
                className = classes.Single(x => x.Id == session.ClassRoomId).Name,
                enteredAtUtc = entered,
                status
            });
        }
        return new
        {
            items
        };
    }

    public async Task<object> Timing(
        Guid actorId,
        DateOnly from,
        DateOnly until,
        Guid yearId,
        Guid? classId,
        Guid? teacherId,
        string? personType,
        string? violation,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        await access.Require(actorId, Permission.ReportView, ct);
        if (until < from || until.DayNumber - from.DayNumber > 400)
            throw new AppException(400, "DATE_RANGE", "Khoảng ngày tối đa 400 ngày.");
        var allowed = await access.ClassIds(actorId, ct);
        if (classId != null)
            await access.Class(actorId, classId.Value, ct);
        var classIds = await db.Classes
            .Where(x => allowed.Contains(x.Id) && x.AcademicYearId == yearId)
            .WhereIf(classId != null, x => x.Id == classId)
            .Select(x => x.Id)
            .ToListAsync(ct);
        var sessions = await db.Sessions.AsNoTracking()
            .Where(x => classIds.Contains(x.ClassRoomId)
                && x.Date >= from
                && x.Date <= until
                && !x.Cancelled
                && x.ClosesAtUtc <= clock.GetUtcNow().UtcDateTime)
            .WhereIf(teacherId != null, session => db.Assignments.Any(assignment => assignment.CatechistId == teacherId
                && assignment.ClassRoomId == session.ClassRoomId
                && assignment.From <= session.Date
                && (assignment.Until == null || session.Date < assignment.Until)))
            .ToListAsync(ct);
        var assignments = await db.Assignments.AsNoTracking()
            .Where(x => classIds.Contains(x.ClassRoomId) && x.From <= until && (x.Until == null || x.Until > from))
            .ToListAsync(ct);
        var sessionIds = sessions.Select(x => x.Id).ToList();
        var teachers = await db.Catechists.AsNoTracking()
            .Where(x => assignments.Select(a => a.CatechistId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var classes = await db.Classes.Where(x => classIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var rows = new List<TimingReportRow>();
        void Add(
            AttendanceSession session,
            Guid id,
            string type,
            string name,
            string? saint,
            string code,
            DateTime input,
            DateTime? output,
            bool adjusted)
        {
            var late = (int)Math.Ceiling(Math.Max(0, (input - session.OpensAtUtc).TotalMinutes));
            var early = output == null ? 0 : (int)Math.Ceiling(Math.Max(0, (session.ClosesAtUtc - output.Value).TotalMinutes));
            if (late == 0 && early == 0)
                return;
            if (violation == "late" && late == 0 || violation == "early" && early == 0)
                return;
            var names = assignments
                .Where(a => a.ClassRoomId == session.ClassRoomId && a.From <= session.Date && (a.Until == null || session.Date < a.Until))
                .Select(a => teachers.GetValueOrDefault(a.CatechistId))
                .Where(x => x != null)
                .Select(x => (x!.SaintName + " " + x.FullName).Trim())
                .Distinct();
            rows.Add(new(
                session.Id,
                id,
                type,
                name,
                saint,
                code,
                session.Date,
                session.ClassRoomId,
                classes[session.ClassRoomId].Name,
                session.OpensAtUtc,
                session.ClosesAtUtc,
                input,
                output,
                late,
                early,
                string.Join(", ", names),
                adjusted));
        }
        if (personType != "Catechist")
        {
            var records = await db.Attendance.Where(x => sessionIds.Contains(x.SessionId) && x.Present).ToListAsync(ct);
            var adjusted = await db.Audit
                .Where(x => x.Action == "AdjustTimes" && records.Select(r => r.Id).Contains(x.EntityId))
                .Select(x => x.EntityId)
                .ToListAsync(ct);
            var people = await db.Students.Where(x => records.Select(r => r.StudentId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            foreach (var record in records)
            {
                var session = sessions.First(x => x.Id == record.SessionId);
                if (session.StudentAttendanceEnabled && people.TryGetValue(record.StudentId, out var person))
                    Add(session, person.Id, "Student", person.FullName, person.SaintName, person.Code, record.RecordedAtUtc, record.CheckedOutAtUtc, adjusted.Contains(record.Id));
            }
        }
        if (personType != "Student")
        {
            var records = await db.CatechistAttendance.Where(x => sessionIds.Contains(x.SessionId)).ToListAsync(ct);
            var adjusted = await db.Audit
                .Where(x => x.Action == "AdjustTimes" && records.Select(r => r.Id).Contains(x.EntityId))
                .Select(x => x.EntityId)
                .ToListAsync(ct);
            var people = await db.Catechists.Where(x => records.Select(r => r.CatechistId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            foreach (var record in records)
            {
                var session = sessions.First(x => x.Id == record.SessionId);
                if (session.CatechistAttendanceEnabled && people.TryGetValue(record.CatechistId, out var person))
                    Add(
                        session,
                        person.Id,
                        "Catechist",
                        person.FullName,
                        person.SaintName,
                        "GLV-" + person.Id.ToString("N")[..8],
                        record.CheckedInAtUtc,
                        record.CheckedOutAtUtc,
                        adjusted.Contains(record.Id));
            }
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return new
        {
            items = rows.OrderBy(x => x.Date).ThenBy(x => x.OpensAtUtc).ThenBy(x => x.FullName).Skip((page - 1) * pageSize).Take(pageSize),
            total = rows.Count,
            totalLateMinutes = rows.Sum(x => x.LateMinutes),
            totalEarlyMinutes = rows.Sum(x => x.EarlyMinutes),
            page,
            pageSize
        };
    }
}
