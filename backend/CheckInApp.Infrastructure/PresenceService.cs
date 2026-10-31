using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public record PresenceInput(DateTime CheckedInAtUtc, DateTime? CheckedOutAtUtc, Guid? Version, string Reason);
public class PresenceService(AppDbContext db, AccessService access, TimeProvider clock)
{
    public async Task<object> List(Guid actor, Guid sessionId, CancellationToken ct)
    {
        await access.Require(actor, Permission.AttendanceView, ct);
        var s = await Session(actor, sessionId, ct);
        if (!s.CatechistAttendanceEnabled) return new { items = Array.Empty<object>() };
        var teachers = await (from a in db.Assignments join c in db.Catechists on a.CatechistId equals c.Id
            where a.ClassRoomId == s.ClassRoomId && a.From <= s.Date && (a.Until == null || s.Date < a.Until)
            select new { c.Id, c.FullName, c.SaintName }).Distinct().ToListAsync(ct);
        var records = await db.CatechistAttendance.AsNoTracking().Where(x => x.SessionId == sessionId).ToListAsync(ct);
        return new { items = teachers.Select(c => new { c.Id, c.FullName, c.SaintName, record = records.SingleOrDefault(r => r.CatechistId == c.Id) }) };
    }
    async Task<AttendanceSession> Session(Guid actor, Guid id, CancellationToken ct)
    {
        var s = await db.Sessions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "NOT_FOUND", "Không có buổi học.");
        await access.Class(actor, s.ClassRoomId, ct);
        return s;
    }
    public async Task Save(Guid actor, Guid sessionId, Guid personId, bool teacher, PresenceInput input, CancellationToken ct)
    {
        await access.Require(actor, teacher ? Permission.CatechistAttendance : Permission.AttendanceCorrect, ct);
        var s = await Session(actor, sessionId, ct);
        if (teacher ? !s.CatechistAttendanceEnabled : !s.StudentAttendanceEnabled)
            throw new AppException(409, "ATTENDANCE_DISABLED", "Buổi này không điểm danh đối tượng đã chọn.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (s.Cancelled || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 500 ||
            input.CheckedInAtUtc.Kind != DateTimeKind.Utc || input.CheckedOutAtUtc is { Kind: not DateTimeKind.Utc } ||
            DateOnly.FromDateTime(input.CheckedInAtUtc.AddHours(7)) != s.Date || input.CheckedInAtUtc > now ||
            (input.CheckedOutAtUtc != null && (input.CheckedOutAtUtc < input.CheckedInAtUtc || input.CheckedOutAtUtc > now || DateOnly.FromDateTime(input.CheckedOutAtUtc.Value.AddHours(7)) != s.Date)))
            throw new AppException(400, "TIME_INVALID", "Giờ vào/ra phải thuộc ngày học, không ở tương lai, giờ ra sau giờ vào; cần lý do.");
        object? before;
        Guid entityId;
        if (teacher)
        {
            if (!await db.Assignments.AnyAsync(x => x.ClassRoomId == s.ClassRoomId && x.CatechistId == personId && x.From <= s.Date && (x.Until == null || s.Date < x.Until), ct))
                throw new AppException(403, "ASSIGNMENT_SCOPE", "Giáo lý viên không phụ trách buổi này.");
            var r = await db.CatechistAttendance.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.CatechistId == personId, ct);
            if (r?.Version != input.Version) throw new AppException(409, "VERSION_CONFLICT", "Dữ liệu đã thay đổi. Tải lại danh sách.");
            before = r == null ? null : new { r.CheckedInAtUtc, r.CheckedOutAtUtc };
            if (r == null) { r = new() { SessionId = sessionId, CatechistId = personId }; db.CatechistAttendance.Add(r); }
            r.CheckedInAtUtc = input.CheckedInAtUtc; r.CheckedOutAtUtc = input.CheckedOutAtUtc; r.ActorId = actor; r.Version = Guid.NewGuid(); entityId = r.Id;
        }
        else
        {
            var r = await db.Attendance.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.StudentId == personId, ct) ?? throw new AppException(400, "CHECKIN_REQUIRED", "Cần điểm danh học sinh trước khi điều chỉnh giờ.");
            if (r.Version != input.Version) throw new AppException(409, "VERSION_CONFLICT", "Dữ liệu đã thay đổi. Tải lại danh sách.");
            before = new { r.RecordedAtUtc, r.CheckedOutAtUtc };
            r.RecordedAtUtc = input.CheckedInAtUtc; r.CheckedOutAtUtc = input.CheckedOutAtUtc; r.Version = Guid.NewGuid(); entityId = r.Id;
        }
        db.Audit.Add(new() { ActorId = actor, EntityId = entityId, EntityType = teacher ? "CatechistAttendance" : "Attendance", Action = "AdjustTimes", Detail = JsonSerializer.Serialize(new { before, after = input }) });
        await db.SaveChangesAsync(ct);
    }
}
