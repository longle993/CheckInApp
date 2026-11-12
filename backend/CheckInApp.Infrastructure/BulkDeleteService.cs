using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public record DeleteSelection(string Kind, Guid[] Ids, string? Fingerprint = null);
public record DeletePreview(string Fingerprint, Dictionary<string, int> Counts, string[] Names);
public class BulkDeleteService(AppDbContext db, AccessService access, ScheduleService schedules)
{
    public async Task<DeletePreview> Run(Guid actor, DeleteSelection input, bool execute, CancellationToken ct = default)
    {
        if (input.Ids == null || input.Ids.Length is < 1 or > 1000 || input.Ids.Distinct().Count() != input.Ids.Length)
            throw new AppException(400, "SELECTION_INVALID", "Chọn từ 1 đến 1.000 mục khác nhau.");
        var permission = input.Kind switch
        {
            "students" => Permission.StudentDelete,
            "catechists" => Permission.CatechistDelete,
            "years" => Permission.YearDelete,
            "classes" => Permission.ClassDelete,
            "schedules" or "sessions" or "schedule-items" => Permission.ScheduleManage,
            _ => throw new AppException(400, "KIND_INVALID", "Loại dữ liệu không hợp lệ.")
        };
        await access.Require(actor, permission, ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var allowed = await access.ClassIds(actor, ct);
        var rights = await access.Rights(actor, ct);
        var ids = input.Ids;
        var years = input.Kind == "years" ? await db.Years.Where(x => ids.Contains(x.Id)).ToListAsync(ct) : [];
        var students = input.Kind == "students" ? await db.Students.Where(x => ids.Contains(x.Id)).ToListAsync(ct) : [];
        var teachers = input.Kind == "catechists" ? await db.Catechists.Where(x => ids.Contains(x.Id)).ToListAsync(ct) : [];
        var classes = input.Kind == "years"
            ? await db.Classes.Where(x => ids.Contains(x.AcademicYearId)).ToListAsync(ct)
            : input.Kind == "classes" ? await db.Classes.Where(x => ids.Contains(x.Id)).ToListAsync(ct) : [];
        var classIds = classes.Select(x => x.Id).ToList();
        var frames = await db.Schedules.Where(x => classIds.Contains(x.ClassRoomId)
            || ((input.Kind == "schedules" || input.Kind == "schedule-items") && ids.Contains(x.Id)))
            .ToListAsync(ct);
        var frameIds = frames.Select(x => x.Id).ToList();
        var sessions = await db.Sessions.Where(x => classIds.Contains(x.ClassRoomId)
            || (x.ScheduleId != null && frameIds.Contains(x.ScheduleId.Value))
            || ((input.Kind == "sessions" || input.Kind == "schedule-items") && ids.Contains(x.Id)))
            .ToListAsync(ct);
        var sessionIds = sessions.Select(x => x.Id).ToList();
        var enrollments = await db.Enrollments.Where(x => classIds.Contains(x.ClassRoomId) || (input.Kind == "students" && ids.Contains(x.StudentId)))
            .ToListAsync(ct);
        var assignments = await db.Assignments.Where(x => classIds.Contains(x.ClassRoomId) || (input.Kind == "catechists" && ids.Contains(x.CatechistId)))
            .ToListAsync(ct);
        var rootCount = input.Kind switch
        {
            "years" => years.Count,
            "classes" => classes.Count,
            "students" => students.Count,
            "catechists" => teachers.Count,
            "schedules" => frames.Count,
            "schedule-items" => frames.Count + sessions.Count(x => ids.Contains(x.Id)),
            _ => sessions.Count
        };
        if (rootCount != ids.Length)
            throw new AppException(409, "SELECTION_CHANGED", "Dữ liệu đã thay đổi. Hãy tải lại danh sách.");
        var affectedClasses = classIds.Concat(frames.Select(x => x.ClassRoomId)).Concat(sessions.Select(x => x.ClassRoomId)).Concat(enrollments.Select(x => x.ClassRoomId))
            .Concat(assignments.Select(x => x.ClassRoomId))
            .Distinct()
            .Order()
            .ToList();
        if (!rights.Global && (affectedClasses.Count == 0 || affectedClasses.Except(allowed).Any()))
            throw new AppException(403, "CLASS_SCOPE", "Bạn không có quyền trên toàn bộ dữ liệu liên quan.");
        foreach (var id in affectedClasses)
            await schedules.LockClass(id, ct);
        var attendance = await db.Attendance.Where(x => sessionIds.Contains(x.SessionId) || (input.Kind == "students" && ids.Contains(x.StudentId)))
            .ToListAsync(ct);
        var teacherAttendance = await db.CatechistAttendance.Where(x => sessionIds.Contains(x.SessionId) || (input.Kind == "catechists" && ids.Contains(x.CatechistId)))
            .ToListAsync(ct);
        var rosters = await db.Rosters.Where(x => sessionIds.Contains(x.SessionId) || (input.Kind == "students" && ids.Contains(x.StudentId)))
            .ToListAsync(ct);
        var credentials = input.Kind == "students" ? await db.Credentials.Where(x => ids.Contains(x.StudentId)).ToListAsync(ct) : [];
        var teacherCredentials = input.Kind == "catechists"
            ? await db.CatechistCredentials.Where(x => ids.Contains(x.CatechistId)).ToListAsync(ct)
            : [];
        var exceptions = await db.Exceptions.Where(x => classIds.Contains(x.ClassRoomId) || (x.ScheduleId != null && frameIds.Contains(x.ScheduleId.Value)))
            .ToListAsync(ct);
        // Include source exceptions in the review fingerprint, but preserve tombstones for individual occurrences.
        var sourceExceptions = (input.Kind == "sessions" || input.Kind == "schedule-items")
            ? await db.Exceptions.Where(x => affectedClasses.Contains(x.ClassRoomId)).ToListAsync(ct)
            : [];
        var recordIds = attendance.Select(x => x.Id).Concat(teacherAttendance.Select(x => x.Id)).ToList();
        var retries = await db.Idempotency.Where(x => recordIds.Contains(x.AttendanceRecordId)).ToListAsync(ct);
        var groups = new Dictionary<string, List<Entity>>
        {
            ["Năm học"] = years.Cast<Entity>().ToList(),
            ["Lớp"] = classes.Cast<Entity>().ToList(),
            ["Học sinh"] = students.Cast<Entity>().ToList(),
            ["GLV"] = teachers.Cast<Entity>().ToList(),
            ["Khung lịch"] = frames.Cast<Entity>().ToList(),
            ["Buổi"] = sessions.Cast<Entity>().ToList(),
            ["Ghi danh"] = enrollments.Cast<Entity>().ToList(),
            ["Phân công"] = assignments.Cast<Entity>().ToList(),
            ["Roster"] = rosters.Cast<Entity>().ToList(),
            ["Điểm danh HS"] = attendance.Cast<Entity>().ToList(),
            ["Điểm danh GLV"] = teacherAttendance.Cast<Entity>().ToList(),
            ["Thẻ HS"] = credentials.Cast<Entity>().ToList(),
            ["Thẻ GLV"] = teacherCredentials.Cast<Entity>().ToList(),
            ["Ngoại lệ"] = exceptions.Cast<Entity>().ToList(),
            ["Yêu cầu điểm danh đã lưu"] = retries.Cast<Entity>().ToList()
        };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            "|",
            groups.Values.SelectMany(x => x).Concat(sourceExceptions).DistinctBy(x => x.Id).OrderBy(x => x.Id).Select(x => JsonSerializer.Serialize(x, x.GetType()))))));
        var names = classes.Select(x => x.Name).Concat(years.Select(x => x.Name)).Concat(students.Select(x => x.FullName)).Concat(teachers.Select(x => x.FullName))
            .Concat((input.Kind == "schedules" || input.Kind == "schedule-items")
            ? frames.Select(x => $"{x.From}–{x.Until} {x.OpensAt}–{x.ClosesAt}")
            : [])
            .Concat((input.Kind == "sessions" || input.Kind == "schedule-items")
            ? sessions.Select(x => $"{x.Date} {x.OpensAtUtc.AddHours(7):HH:mm}")
            : [])
            .ToArray();
        var result = new DeletePreview(
            fingerprint,
            groups.Where(x => x.Value.Count > 0).ToDictionary(x => x.Key, x => x.Value.Count),
            names);
        if (!execute)
            return result;
        if (input.Fingerprint != fingerprint)
            throw new AppException(409, "DELETE_CHANGED", "Dữ liệu liên quan đã thay đổi. Hãy xem lại trước khi xóa.");
        if (input.Kind is "sessions" or "schedule-items")
            foreach (var s in sessions.Where(x => x.ScheduleId == null || !frameIds.Contains(x.ScheduleId.Value)))
            {
                var source = s.SourceDate ?? s.Date;
                var ex = sourceExceptions.SingleOrDefault(x => x.ClassRoomId == s.ClassRoomId && x.ScheduleId == s.ScheduleId && x.Date == source);
                if (ex == null)
                {
                    ex = new()
                    {
                        ClassRoomId = s.ClassRoomId,
                        ScheduleId = s.ScheduleId,
                        Date = source
                    };
                    db.Exceptions.Add(ex);
                }
                ex.Cancelled = true;
                ex.MovedToDate = null;
                ex.Reason = "Đã xóa buổi điểm danh";
            }
        db.RemoveRange(retries);
        db.RemoveRange(attendance);
        db.RemoveRange(teacherAttendance);
        db.RemoveRange(rosters);
        db.RemoveRange(credentials);
        db.RemoveRange(teacherCredentials);
        db.RemoveRange(enrollments);
        db.RemoveRange(assignments);
        db.RemoveRange(exceptions);
        db.RemoveRange(sessions);
        db.RemoveRange(frames);
        db.RemoveRange(students);
        db.RemoveRange(teachers);
        db.RemoveRange(classes);
        db.RemoveRange(years);
        // Invalidate encrypted card retry caches containing deleted people without erasing security audit.
        if (input.Kind is "students" or "catechists")
        {
            var avatars = await db.Audit.Where(x => x.Action == "Avatar" && x.EntityType == input.Kind && ids.Contains(x.EntityId) && x.Detail != "")
                .ToListAsync(ct);
            foreach (var file in avatars.Select(x => x.Detail).Distinct())
                db.Audit.Add(new()
                {
                    ActorId = actor,
                    EntityType = input.Kind,
                    Action = "AvatarCleanupPending",
                    Detail = file
                });
            var batches = await db.Audit.Where(x => x.Action == "BatchCards" && x.EntityType == input.Kind).ToListAsync(ct);
            foreach (var batch in batches)
                if (ids.Any(id => batch.Detail.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase)))
                {
                    batch.Action = "BatchCardsInvalidated";
                    batch.Detail = "Hồ sơ trong lô đã xóa";
                }
            foreach (var id in ids)
                db.Audit.Add(new()
                {
                    ActorId = actor,
                    EntityId = id,
                    EntityType = input.Kind,
                    Action = "Avatar",
                    Detail = ""
                });
        }
        db.Audit.Add(new()
        {
            ActorId = actor,
            EntityType = input.Kind,
            Action = "BulkDelete",
            Detail = JsonSerializer.Serialize(new
            {
                fingerprint,
                counts = result.Counts
            })
        });
        foreach (var id in ids)
            db.Audit.Add(new()
            {
                ActorId = actor,
                EntityId = id,
                EntityType = input.Kind,
                Action = "BulkDeleteItem",
                Detail = fingerprint
            });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
