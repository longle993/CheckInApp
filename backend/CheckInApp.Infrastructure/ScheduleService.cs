using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public class ScheduleService(AppDbContext db, TimeProvider clock, AccessService? access = null) : IScheduleService
{
    public Task EditSession(Guid actor, Guid id, SessionScheduleInput input, CancellationToken ct) =>
        EditSession(actor, id, input, access ?? throw new InvalidOperationException("AccessService is required for session edits."), ct);

    public async Task EditSession(Guid actor, Guid id, SessionScheduleInput input, AccessService accessService, CancellationToken ct)
    {
        await accessService.Require(actor, Permission.ScheduleManage, ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await LockClass((await db.Sessions.AsNoTracking().SingleAsync(x => x.Id == id, ct)).ClassRoomId, ct);
        var query = db.Database.IsSqlServer()
            ? db.Sessions.FromSqlInterpolated($"SELECT * FROM [Sessions] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
            : db.Sessions.Where(x => x.Id == id);
        var session = await query.SingleOrDefaultAsync(ct) ?? throw new AppException(404, "NOT_FOUND", "Không có buổi học.");
        await accessService.Class(actor, session.ClassRoomId, ct);
        var originalStart = session.OpensAtUtc;
        var targetDate = input.Date ?? session.Date;
        var year = await (from c in db.Classes
                          join y in db.Years on c.AcademicYearId equals y.Id
                          where c.Id == session.ClassRoomId
                          select y).SingleAsync(ct);
        if (targetDate < year.StartDate || targetDate > year.EndDate)
            throw new AppException(400, "YEAR_RANGE", "Ngày học phải nằm trong năm học của lớp.");
        if (targetDate != session.Date)
        {
            var targetIds = db.Sessions.Where(x => x.ClassRoomId == session.ClassRoomId && x.Date == targetDate && x.Id != id).Select(x => x.Id);
            if (await db.Attendance.AnyAsync(x => targetIds.Contains(x.SessionId), ct)
                || await db.CatechistAttendance.AnyAsync(x => targetIds.Contains(x.SessionId), ct))
                throw new AppException(
                    409,
                    "TARGET_DATE_HAS_DATA",
                    "Ngày chuyển đến đã phát sinh dữ liệu điểm danh. Hãy chọn ngày khác để tránh thay đổi lịch sử.");
        }
        var newStart = AttendanceRules.ToUtc(targetDate, input.OpensAt);
        var newEnd = AttendanceRules.ToUtc(targetDate, input.ClosesAt);
        if (await db.Sessions.AnyAsync(
            x => x.ClassRoomId == session.ClassRoomId
            && x.Id != id
            && !x.Cancelled
            && x.OpensAtUtc < newEnd
            && newStart < x.ClosesAtUtc,
            ct))
            throw new AppException(409, "SCHEDULE_OVERLAP", "Buổi học trùng thời gian với một buổi khác của lớp.");
        void ValidateTime()
        {
            var now = clock.GetUtcNow().UtcDateTime;
            if (session.Cancelled || originalStart <= now)
                throw new AppException(409, "SESSION_STARTED", "Chỉ được sửa buổi chưa bắt đầu và chưa hủy.");
            if (newStart <= now || newEnd <= newStart)
                throw new AppException(
                    400,
                    "SCHEDULE_TIME_INVALID",
                    "Giờ bắt đầu mới phải lớn hơn hiện tại; giờ kết thúc phải sau giờ bắt đầu trong cùng ngày.");
        }
        ValidateTime();
        if (session.Version != input.Version)
            throw new AppException(409, "VERSION_CONFLICT", "Lịch đã thay đổi. Hãy tải lại.");
        if (!input.StudentAttendanceEnabled && !input.CatechistAttendanceEnabled
            || string.IsNullOrWhiteSpace(input.Reason)
            || input.Reason.Length > 500)
            throw new AppException(400, "VALIDATION", "Chọn HS/GLV và nhập lý do tối đa 500 ký tự.");
        if (await db.Attendance.AnyAsync(x => x.SessionId == id, ct) || await db.CatechistAttendance.AnyAsync(x => x.SessionId == id, ct))
            throw new AppException(409, "ATTENDANCE_EXISTS", "Buổi đã có dữ liệu điểm danh, không thể chỉnh lịch.");
        var before = new
        {
            session.OpensAtUtc,
            session.ClosesAtUtc,
            session.StudentAttendanceEnabled,
            session.CatechistAttendanceEnabled
        };
        var exception = await db.Exceptions.SingleOrDefaultAsync(
            x => x.ClassRoomId == session.ClassRoomId
            && x.Date == (session.SourceDate ?? session.Date)
            && x.ScheduleId == session.ScheduleId,
            ct);
        if (exception == null)
        {
            exception = new()
            {
                ClassRoomId = session.ClassRoomId,
                Date = session.SourceDate ?? session.Date,
                ScheduleId = session.ScheduleId
            };
            db.Exceptions.Add(exception);
        }
        exception.MovedToDate = targetDate;
        session.SourceDate ??= session.Date;
        session.Date = targetDate;
        exception.OpensAt = input.OpensAt;
        exception.ClosesAt = input.ClosesAt;
        exception.StudentAttendanceEnabled = input.StudentAttendanceEnabled;
        exception.CatechistAttendanceEnabled = input.CatechistAttendanceEnabled;
        exception.Cancelled = false;
        exception.Reason = input.Reason.Trim();
        session.OpensAtUtc = newStart;
        session.ClosesAtUtc = newEnd;
        session.StudentAttendanceEnabled = input.StudentAttendanceEnabled;
        session.CatechistAttendanceEnabled = input.CatechistAttendanceEnabled;
        session.Version = Guid.NewGuid();
        db.Rosters.RemoveRange(await db.Rosters.Where(x => x.SessionId == id).ToListAsync(ct));
        if (input.StudentAttendanceEnabled)
            foreach (var e in await db.Enrollments.Where(x => x.ClassRoomId == session.ClassRoomId && x.From <= session.Date && (x.Until == null || session.Date < x.Until))
                .ToListAsync(ct))
                db.Rosters.Add(new()
                {
                    SessionId = id,
                    StudentId = e.StudentId,
                    EnrollmentId = e.Id
                });
        db.Audit.Add(new()
        {
            ActorId = actor,
            EntityId = id,
            EntityType = "AttendanceSession",
            Action = "EditSchedule",
            Detail = System.Text.Json.JsonSerializer.Serialize(new
            {
                before,
                after = input
            })
        });
        ValidateTime();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task LockClass(Guid id, CancellationToken ct)
    {
        if (db.Database.IsSqlServer())
            await db.Classes.FromSqlInterpolated($"SELECT * FROM [Classes] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").SingleAsync(ct);
    }
    public async Task Synchronize(Guid classId, CancellationToken ct = default)
    {
        await using var own = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        await LockClass(classId, ct);
        var schedules = await db.Schedules.Where(x => x.ClassRoomId == classId).ToListAsync(ct);
        var all = await db.Sessions.Where(x => x.ClassRoomId == classId).ToListAsync(ct);
        var exceptions = await db.Exceptions.Where(x => x.ClassRoomId == classId).ToListAsync(ct);
        var enrollments = await db.Enrollments.Where(x => x.ClassRoomId == classId).ToListAsync(ct);
        foreach (var schedule in schedules)
        {
            var existing = all.Where(x => x.ScheduleId == schedule.Id || (x.ScheduleId == null && schedules.Count == 1)).ToList();
            for (var date = schedule.From; date <= schedule.Until; date = date.AddDays(1))
            {
                var ex = exceptions.SingleOrDefault(x => x.Date == date && (x.ScheduleId == schedule.Id || (x.ScheduleId == null && schedules.Count == 1)));
                var included = ex != null ? !ex.Cancelled : AttendanceRules.Scheduled(date, schedule.DaysMask);
                var session = existing.SingleOrDefault(x => (x.SourceDate ?? x.Date) == date);
                if (session == null && !included)
                    continue;
                var actual = ex?.MovedToDate ?? date;
                var start = AttendanceRules.ToUtc(actual, ex?.OpensAt ?? schedule.OpensAt);
                var end = AttendanceRules.ToUtc(actual, ex?.ClosesAt ?? schedule.ClosesAt);
                if (session != null && session.OpensAtUtc <= clock.GetUtcNow().UtcDateTime)
                    continue;
                if (session != null && included && start <= clock.GetUtcNow().UtcDateTime)
                    throw new AppException(400, "SCHEDULE_TIME_INVALID", "Không được chuyển buổi tương lai về quá khứ.");
                if (session != null
                    && (!included || session.Date != actual || session.OpensAtUtc != start || session.ClosesAtUtc != end)
                    && (await db.Attendance.AnyAsync(x => x.SessionId == session.Id, ct)
                    || await db.CatechistAttendance.AnyAsync(x => x.SessionId == session.Id, ct)))
                    throw new AppException(
                        409,
                        "ATTENDANCE_EXISTS",
                        "Buổi bị thay đổi đã phát sinh dữ liệu điểm danh, không thể chỉnh lịch.");
                if (session == null)
                {
                    session = new()
                    {
                        ClassRoomId = classId
                    };
                    db.Sessions.Add(session);
                    all.Add(session);
                }
                session.ScheduleId = schedule.Id;
                session.SourceDate = date;
                session.Date = actual;
                session.OpensAtUtc = start;
                session.ClosesAtUtc = end;
                session.Cancelled = !included;
                session.ScheduleVersion = schedule.Version;
                session.Version = Guid.NewGuid();
                session.StudentAttendanceEnabled = ex?.StudentAttendanceEnabled ?? schedule.StudentAttendanceEnabled;
                session.CatechistAttendanceEnabled = ex?.CatechistAttendanceEnabled ?? schedule.CatechistAttendanceEnabled;
                db.Rosters.RemoveRange(await db.Rosters.Where(x => x.SessionId == session.Id).ToListAsync(ct));
                if (included && session.StudentAttendanceEnabled)
                    foreach (var e in enrollments.Where(e => AttendanceRules.Eligible(actual, e.From, e.Until)))
                        db.Rosters.Add(new()
                        {
                            SessionId = session.Id,
                            StudentId = e.StudentId,
                            EnrollmentId = e.Id
                        });
            }
            foreach (var session in existing.Where(x => x.OpensAtUtc > clock.GetUtcNow().UtcDateTime
                && ((x.SourceDate ?? x.Date) < schedule.From || (x.SourceDate ?? x.Date) > schedule.Until)))
            {
                session.Cancelled = true;
                session.Version = Guid.NewGuid();
            }
        }
        foreach (var group in all.Where(x => !x.Cancelled).GroupBy(x => x.Date))
        {
            var sorted = group.OrderBy(x => x.OpensAtUtc).ToList();
            for (var i = 1; i < sorted.Count; i++)
                if (sorted[i].OpensAtUtc < sorted[i - 1].ClosesAtUtc)
                    throw new AppException(409, "SCHEDULE_OVERLAP", "Các buổi cùng lớp không được chồng thời gian.");
        }
        var cancelled = all.Where(x => x.Cancelled && x.OpensAtUtc > clock.GetUtcNow().UtcDateTime).ToList();
        foreach (var removed in cancelled)
        {
            if (await db.Attendance.AnyAsync(x => x.SessionId == removed.Id, ct)
                || await db.CatechistAttendance.AnyAsync(x => x.SessionId == removed.Id, ct))
                continue;
            db.Rosters.RemoveRange(await db.Rosters.Where(x => x.SessionId == removed.Id).ToListAsync(ct));
            db.Sessions.Remove(removed);
        }
        await db.SaveChangesAsync(ct);
        if (own != null)
            await own.CommitAsync(ct);
    }
}
