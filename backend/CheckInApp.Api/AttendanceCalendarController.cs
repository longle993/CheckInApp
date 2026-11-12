using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/attendance-calendar")]
public class AttendanceCalendarController(AppDbContext db, AccessService access, TimeProvider clock) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet]
    public async Task<object> Get(Guid yearId, string kind, Guid personId, int year, int month, Guid? classId, CancellationToken ct)
    {
        await access.Require(Actor, Permission.AttendanceView, ct);
        if (kind is not ("students" or "catechists") || year < 1 || year > 9998 || month < 1 || month > 12)
            throw new AppException(400, "CALENDAR_INVALID", "Tháng, năm hoặc đối tượng không hợp lệ.");
        await access.Require(Actor, kind == "students" ? Permission.StudentView : Permission.CatechistView, ct);
        var allowed = await access.ClassIds(Actor, ct);
        if (classId != null)
            await access.Class(Actor, classId.Value, ct);
        var classes = await db.Classes.Where(x => allowed.Contains(x.Id) && x.AcademicYearId == yearId && (classId == null || x.Id == classId))
            .ToListAsync(ct);
        var ids = classes.Select(x => x.Id).ToList();
        var from = new DateOnly(year, month, 1);
        var until = from.AddMonths(1);
        var sessions = await db.Sessions.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId) && x.Date >= from && x.Date < until).OrderBy(x => x.OpensAtUtc)
            .ToListAsync(ct);
        var sessionIds = sessions.Select(x => x.Id).ToList();
        var now = clock.GetUtcNow().UtcDateTime;
        var items = new List<object>();
        var rosters = await db.Rosters.Where(x => x.StudentId == personId && sessionIds.Contains(x.SessionId)).Select(x => x.SessionId).ToListAsync(ct);
        var enrollments = await db.Enrollments.Where(x => x.StudentId == personId && ids.Contains(x.ClassRoomId)).ToListAsync(ct);
        var assignments = await db.Assignments.Where(x => x.CatechistId == personId && ids.Contains(x.ClassRoomId)).ToListAsync(ct);
        if (kind == "students" ? enrollments.Count == 0 && rosters.Count == 0 : assignments.Count == 0)
            throw new AppException(403, "PERSON_SCOPE", "Người được chọn không thuộc phạm vi xem.");
        var records = await db.Attendance.Where(x => x.StudentId == personId && sessionIds.Contains(x.SessionId) && x.Present).ToListAsync(ct);
        var teacherRecords = await db.CatechistAttendance.Where(x => x.CatechistId == personId && sessionIds.Contains(x.SessionId)).ToListAsync(ct);
        foreach (var s in sessions)
        {
            var eligible = kind == "students"
                ? s.StudentAttendanceEnabled
                && (rosters.Contains(s.Id)
                || (s.Cancelled
                && enrollments.Any(x => x.ClassRoomId == s.ClassRoomId && AttendanceRules.Eligible(s.Date, x.From, x.Until))))
                : s.CatechistAttendanceEnabled
                && assignments.Any(x => x.ClassRoomId == s.ClassRoomId && AttendanceRules.Eligible(s.Date, x.From, x.Until));
            if (!eligible)
                continue;
            DateTime? entered = kind == "students"
                ? records.FirstOrDefault(x => x.SessionId == s.Id)?.RecordedAtUtc
                : teacherRecords.FirstOrDefault(x => x.SessionId == s.Id)?.CheckedInAtUtc;
            var status = s.Cancelled
                ? "cancelled"
                : entered == DateTime.MinValue ? "unknown" : entered != null ? (entered > s.OpensAtUtc ? "late" : "ontime") : now < s.OpensAtUtc ? "future" : now >= s.ClosesAtUtc ? "absent" : "pending";
            items.Add(new
            {
                s.Id,
                s.Date,
                s.OpensAtUtc,
                s.ClosesAtUtc,
                className = classes.Single(x => x.Id == s.ClassRoomId).Name,
                enteredAtUtc = entered,
                status
            });
        }
        return new
        {
            items
        };
    }
}
