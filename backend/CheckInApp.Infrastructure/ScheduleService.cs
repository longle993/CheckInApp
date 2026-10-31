using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public class ScheduleService(AppDbContext db, TimeProvider clock)
{
    public async Task EditSession(Guid actor, Guid id, SessionScheduleInput input, AccessService access, CancellationToken ct)
    {
        await access.Require(actor,Permission.ScheduleManage,ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var query=db.Database.IsSqlServer()?db.Sessions.FromSqlInterpolated($"SELECT * FROM [Sessions] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}"):db.Sessions.Where(x=>x.Id==id);
        var session=await query.SingleOrDefaultAsync(ct)??throw new AppException(404,"NOT_FOUND","Không có buổi học.");
        await access.Class(actor,session.ClassRoomId,ct);
        var originalStart=session.OpensAtUtc;
        var newStart=AttendanceRules.ToUtc(session.Date,input.OpensAt);var newEnd=AttendanceRules.ToUtc(session.Date,input.ClosesAt);
        void ValidateTime(){var now=clock.GetUtcNow().UtcDateTime;if(session.Cancelled || originalStart<=now)throw new AppException(409,"SESSION_STARTED","Chỉ được sửa buổi chưa bắt đầu và chưa hủy.");if(newStart<=now || newEnd<=newStart)throw new AppException(400,"SCHEDULE_TIME_INVALID","Giờ bắt đầu mới phải lớn hơn hiện tại; giờ kết thúc phải sau giờ bắt đầu trong cùng ngày.");}
        ValidateTime();
        if(session.Version!=input.Version)throw new AppException(409,"VERSION_CONFLICT","Lịch đã thay đổi. Hãy tải lại.");
        if(!input.StudentAttendanceEnabled&&!input.CatechistAttendanceEnabled || string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Length>500)throw new AppException(400,"VALIDATION","Chọn HS/GLV và nhập lý do tối đa 500 ký tự.");
        if(await db.Attendance.AnyAsync(x=>x.SessionId==id,ct)||await db.CatechistAttendance.AnyAsync(x=>x.SessionId==id,ct))throw new AppException(409,"ATTENDANCE_EXISTS","Buổi đã có dữ liệu điểm danh, không thể chỉnh lịch.");
        var before=new {session.OpensAtUtc,session.ClosesAtUtc,session.StudentAttendanceEnabled,session.CatechistAttendanceEnabled};
        var exception=await db.Exceptions.SingleOrDefaultAsync(x=>x.ClassRoomId==session.ClassRoomId&&x.Date==session.Date,ct);
        if(exception==null){exception=new(){ClassRoomId=session.ClassRoomId,Date=session.Date};db.Exceptions.Add(exception);}
        exception.OpensAt=input.OpensAt;exception.ClosesAt=input.ClosesAt;exception.StudentAttendanceEnabled=input.StudentAttendanceEnabled;exception.CatechistAttendanceEnabled=input.CatechistAttendanceEnabled;exception.Cancelled=false;exception.Reason=input.Reason.Trim();
        session.OpensAtUtc=newStart;session.ClosesAtUtc=newEnd;session.StudentAttendanceEnabled=input.StudentAttendanceEnabled;session.CatechistAttendanceEnabled=input.CatechistAttendanceEnabled;session.Version=Guid.NewGuid();
        db.Rosters.RemoveRange(await db.Rosters.Where(x=>x.SessionId==id).ToListAsync(ct));
        if(input.StudentAttendanceEnabled)foreach(var e in await db.Enrollments.Where(x=>x.ClassRoomId==session.ClassRoomId&&x.From<=session.Date&&(x.Until==null||session.Date<x.Until)).ToListAsync(ct))db.Rosters.Add(new(){SessionId=id,StudentId=e.StudentId,EnrollmentId=e.Id});
        db.Audit.Add(new(){ActorId=actor,EntityId=id,EntityType="AttendanceSession",Action="EditSchedule",Detail=System.Text.Json.JsonSerializer.Serialize(new {before,after=input})});
        ValidateTime();await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task Synchronize(Guid classId, CancellationToken ct = default)
    {
        var schedule = await db.Schedules.SingleOrDefaultAsync(x => x.ClassRoomId == classId, ct);
        if (schedule == null)
            return;
        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await db.Sessions.Where(x => x.ClassRoomId == classId).ToListAsync(ct);
        var exceptions = await db.Exceptions.Where(x => x.ClassRoomId == classId).ToDictionaryAsync(x => x.Date, ct);
        var enrollments = await db.Enrollments.Where(x => x.ClassRoomId == classId).ToListAsync(ct);
        var days = Enumerable.Range(0, schedule.Until.DayNumber - schedule.From.DayNumber + 1).Select(i => schedule.From.AddDays(i));
        foreach (var date in days)
        {
            exceptions.TryGetValue(date, out var ex);
            var included = ex != null ? !ex.Cancelled : AttendanceRules.Scheduled(date, schedule.DaysMask);
            var session = existing.SingleOrDefault(x => x.Date == date);
            if (!included && session == null)
                continue;
            if (session == null)
            {
                session = new()
                {
                    ClassRoomId = classId,
                    Date = date,
                    OpensAtUtc = AttendanceRules.ToUtc(date, ex?.OpensAt ?? schedule.OpensAt),
                    ClosesAtUtc = AttendanceRules.ToUtc(date, ex?.ClosesAt ?? schedule.ClosesAt),
                    ScheduleVersion = schedule.Version
                };
                db.Sessions.Add(session);
            }
            else if (session.OpensAtUtc > now)
            {
                if(included && AttendanceRules.ToUtc(date,ex?.OpensAt??schedule.OpensAt)<=clock.GetUtcNow().UtcDateTime)
                    throw new AppException(400,"SCHEDULE_TIME_INVALID","Không thể chuyển giờ bắt đầu của buổi chưa mở về hiện tại hoặc quá khứ.");
                session.Cancelled = !included;
                session.OpensAtUtc = AttendanceRules.ToUtc(date, ex?.OpensAt ?? schedule.OpensAt);
                session.ClosesAtUtc = AttendanceRules.ToUtc(date, ex?.ClosesAt ?? schedule.ClosesAt);
                session.ScheduleVersion = schedule.Version;
                session.Version = Guid.NewGuid();
            }
            else
                continue; // Never silently rebuild a historical or already-open roster.
            session.StudentAttendanceEnabled = ex?.StudentAttendanceEnabled ?? schedule.StudentAttendanceEnabled;
            session.CatechistAttendanceEnabled = ex?.CatechistAttendanceEnabled ?? schedule.CatechistAttendanceEnabled;
            var old = await db.Rosters.Where(x => x.SessionId == session.Id).ToListAsync(ct);
            db.Rosters.RemoveRange(old);
            if (!session.Cancelled && session.StudentAttendanceEnabled)
            foreach (var e in enrollments.Where(e => AttendanceRules.Eligible(date, e.From, e.Until)))
                db.Rosters.Add(new()
                {
                    SessionId = session.Id,
                    StudentId = e.StudentId,
                    EnrollmentId = e.Id
                });
        }
        foreach (var session in existing.Where(s => s.OpensAtUtc > now && (s.Date < schedule.From || s.Date > schedule.Until)))
        {
            session.Cancelled = true;
            session.Version = Guid.NewGuid();
        }
        await db.SaveChangesAsync(ct);
    }
}
public record SessionScheduleInput(TimeOnly OpensAt, TimeOnly ClosesAt, bool StudentAttendanceEnabled, bool CatechistAttendanceEnabled, Guid Version, string Reason);
