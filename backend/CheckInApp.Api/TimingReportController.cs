using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/reports/timing")]
public class TimingReportController(AppDbContext db, AccessService access, TimeProvider clock) : ControllerBase
{
    public record Row(
        Guid SessionId,
        Guid PersonId,
        string PersonType,
        string FullName,
        string? SaintName,
        string Code,
        DateOnly Date,
        Guid ClassId,
        string ClassName,
        DateTime OpensAtUtc,
        DateTime ClosesAtUtc,
        DateTime InAtUtc,
        DateTime? OutAtUtc,
        int LateMinutes,
        int EarlyMinutes,
        string Teachers,
        bool Adjusted);
    [HttpGet]
    public async Task<object> Get(
        DateOnly from,
        DateOnly until,
        Guid yearId,
        Guid? classId,
        Guid? teacherId,
        string? personType,
        string? violation,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var actor = Guid.Parse(User.FindFirst("sub")!.Value);
        await access.Require(actor, Permission.ReportView, ct);
        if (until < from || until.DayNumber - from.DayNumber > 400)
            throw new AppException(400, "DATE_RANGE", "Khoảng ngày tối đa 400 ngày.");
        var ids = await access.ClassIds(actor, ct);
        if (classId != null)
        {
            await access.Class(actor, classId.Value, ct);
            ids = ids.Where(x => x == classId).ToList();
        }
        ids = await db.Classes.Where(x => ids.Contains(x.Id) && x.AcademicYearId == yearId).Select(x => x.Id).ToListAsync(ct);
        var sessions = await db.Sessions.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId)
            && x.Date >= from
            && x.Date <= until
            && !x.Cancelled
            && x.ClosesAtUtc <= clock.GetUtcNow().UtcDateTime)
            .ToListAsync(ct);
        var assignments = await db.Assignments.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId) && x.From <= until && (x.Until == null || x.Until > from))
            .ToListAsync(ct);
        if (teacherId != null)
            sessions = sessions.Where(s => assignments.Any(a => a.CatechistId == teacherId
                && a.ClassRoomId == s.ClassRoomId
                && a.From <= s.Date
                && (a.Until == null || s.Date < a.Until)))
                .ToList();
        var sessionIds = sessions.Select(x => x.Id).ToList();
        var teachers = await db.Catechists.AsNoTracking().Where(x => assignments.Select(a => a.CatechistId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var classes = await db.Classes.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var rows = new List<Row>();
        void Add(
            AttendanceSession s,
            Guid id,
            string type,
            string name,
            string? saint,
            string code,
            DateTime input,
            DateTime? output,
            bool adjusted)
        {
            var late = (int)Math.Ceiling(Math.Max(0, (input - s.OpensAtUtc).TotalMinutes));
            var early = output == null ? 0 : (int)Math.Ceiling(Math.Max(0, (s.ClosesAtUtc - output.Value).TotalMinutes));
            if (late == 0 && early == 0)
                return;
            if (violation == "late" && late == 0 || violation == "early" && early == 0)
                return;
            var names = assignments.Where(a => a.ClassRoomId == s.ClassRoomId && a.From <= s.Date && (a.Until == null || s.Date < a.Until)).Select(a => teachers.GetValueOrDefault(a.CatechistId))
                .Where(x => x != null)
                .Select(x => (x!.SaintName + " " + x.FullName).Trim())
                .Distinct();
            rows.Add(new(
                s.Id,
                id,
                type,
                name,
                saint,
                code,
                s.Date,
                s.ClassRoomId,
                classes[s.ClassRoomId].Name,
                s.OpensAtUtc,
                s.ClosesAtUtc,
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
            var adjusted = await db.Audit.Where(x => x.Action == "AdjustTimes" && records.Select(r => r.Id).Contains(x.EntityId)).Select(x => x.EntityId)
                .ToListAsync(ct);
            var people = await db.Students.Where(x => records.Select(r => r.StudentId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            foreach (var r in records)
            {
                var s = sessions.First(x => x.Id == r.SessionId);
                if (s.StudentAttendanceEnabled && people.TryGetValue(r.StudentId, out var p))
                    Add(s, p.Id, "Student", p.FullName, p.SaintName, p.Code, r.RecordedAtUtc, r.CheckedOutAtUtc, adjusted.Contains(r.Id));
            }
        }
        if (personType != "Student")
        {
            var records = await db.CatechistAttendance.Where(x => sessionIds.Contains(x.SessionId)).ToListAsync(ct);
            var adjusted = await db.Audit.Where(x => x.Action == "AdjustTimes" && records.Select(r => r.Id).Contains(x.EntityId)).Select(x => x.EntityId)
                .ToListAsync(ct);
            var people = await db.Catechists.Where(x => records.Select(r => r.CatechistId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            foreach (var r in records)
            {
                var s = sessions.First(x => x.Id == r.SessionId);
                if (s.CatechistAttendanceEnabled && people.TryGetValue(r.CatechistId, out var p))
                    Add(
                        s,
                        p.Id,
                        "Catechist",
                        p.FullName,
                        p.SaintName,
                        "GLV-" + p.Id.ToString("N")[..8],
                        r.CheckedInAtUtc,
                        r.CheckedOutAtUtc,
                        adjusted.Contains(r.Id));
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
