using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
namespace CheckInApp.Infrastructure;

public class AttendanceService(AppDbContext db, AccessService access, TimeProvider clock, RedisCache cache) : IAttendanceService
{
    public async Task<CheckInResult> CheckIn(CheckInCommand c, CancellationToken ct)
    {
        await access.Require(c.ActorId, Permission.CheckIn, ct);
        if (c.Input.PersonType != null
            && (c.Input.PersonType is not ("Student" or "Catechist")
            || (c.Input.PersonType == "Catechist") != c.Input.Credential.Trim().StartsWith("CG1-", StringComparison.Ordinal)))
            throw new AppException(400, "WRONG_CARD_TYPE", "Loại thẻ không đúng màn điểm danh.");
        if (c.Input.Credential.Trim().StartsWith("CG1-", StringComparison.Ordinal))
            await access.Require(c.ActorId, Permission.CatechistAttendance, ct);
        if (c.Input.Credential.Length is < 8 or > 256 || c.Input.ClientRequestId.Length is < 8 or > 100)
            throw new AppException(400, "INVALID_SCAN", "Mã thẻ hoặc mã yêu cầu không hợp lệ.");
        if (c.Input.Method is not ("QR" or "Barcode" or "Manual"))
            throw new AppException(400, "INVALID_METHOD", "Phương thức điểm danh không hợp lệ.");
        if (c.Input.Method == "Manual")
            await access.Require(c.ActorId, Permission.AttendanceCorrect, ct);
        for (var attempt = 0; ; attempt++)
            try
            {
                return await Attempt(c, ct);
            }
            catch (Exception e) when (attempt < 4 && Retryable(e))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(50 * (attempt + 1) + Random.Shared.Next(20, 100), ct);
            }
    }
    private static bool Retryable(Exception error)
    {
        if (error is DbUpdateException)
            return true;
        for (Exception? current = error; current != null; current = current.InnerException)
            if (current is SqlException { Number: 1205 })
                return true;
        return false;
    }
    private async Task<CheckInResult> Attempt(CheckInCommand c, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        // SQL Server: take an update lock before reading the session to avoid
        // shared-to-exclusive lock conversion deadlocks across concurrent scans.
        var sessionQuery = db.Database.IsSqlServer()
            ? db.Sessions.FromSqlInterpolated($"SELECT * FROM [Sessions] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {c.SessionId}")
            : db.Sessions.Where(x => x.Id == c.SessionId);
        var session = await sessionQuery.SingleOrDefaultAsync(ct) ?? throw new AppException(404, "SESSION_NOT_FOUND", "Không có buổi điểm danh.");
        await access.Class(c.ActorId, session.ClassRoomId, ct);
        var payloadHash = AuthService.Hash(c.SessionId + "|" + c.Input.Credential + "|" + c.Input.Method);
        var previous = await db.Idempotency.SingleOrDefaultAsync(x => x.ActorId == c.ActorId && x.Key == c.Input.ClientRequestId, ct);
        if (previous != null)
        {
            if (previous.PayloadHash != payloadHash)
                throw new AppException(409, "IDEMPOTENCY_CONFLICT", "Mã yêu cầu đã được dùng cho nội dung khác.");
            return JsonSerializer.Deserialize<CheckInResult>(previous.ResponseJson)!;
        }
        var now = clock.GetUtcNow().UtcDateTime;
        if (session.Cancelled || now < session.OpensAtUtc || now >= session.ClosesAtUtc)
            throw new AppException(409, "SESSION_CLOSED", "Buổi chưa mở, đã đóng hoặc đã hủy.");
        if (c.Input.Credential.Trim().StartsWith("CG1-", StringComparison.Ordinal))
        {
            if (!session.CatechistAttendanceEnabled)
                throw new AppException(409, "CATECHIST_ATTENDANCE_DISABLED", "Buổi này không điểm danh GLV.");
            var teacherHash = AuthService.Hash(c.Input.Credential.Trim());
            var card = await db.CatechistCredentials.SingleOrDefaultAsync(x => x.TokenHash == teacherHash && x.RevokedAtUtc == null, ct)
                ?? throw new AppException(400, "CREDENTIAL_INVALID", "Thẻ không hợp lệ hoặc đã thu hồi.");
            if (!await db.Assignments.AnyAsync(
                x => x.CatechistId == card.CatechistId
                && x.ClassRoomId == session.ClassRoomId
                && x.From <= session.Date
                && (x.Until == null || session.Date < x.Until),
                ct))
                throw new AppException(409, "WRONG_CLASS", "Giáo lý viên không phụ trách buổi này.");
            var teacher = await db.Catechists.SingleAsync(x => x.Id == card.CatechistId, ct);
            var attendance = await db.CatechistAttendance.SingleOrDefaultAsync(x => x.SessionId == session.Id && x.CatechistId == teacher.Id, ct);
            var repeated = attendance != null;
            if (attendance == null)
            {
                attendance = new()
                {
                    SessionId = session.Id,
                    CatechistId = teacher.Id,
                    CheckedInAtUtc = now,
                    ActorId = c.ActorId
                };
                db.CatechistAttendance.Add(attendance);
            }
            session.Version = Guid.NewGuid();
            var response = new CheckInResult(
                repeated ? "AlreadyCheckedIn" : "Created",
                attendance.Id,
                teacher.Id,
                ((teacher.SaintName ?? "") + " " + teacher.FullName).Trim(),
                "GLV-" + teacher.Id.ToString("N")[..8].ToUpperInvariant(),
                true,
                attendance.CheckedInAtUtc,
                attendance.Version,
                "Catechist");
            db.Idempotency.Add(new()
            {
                ActorId = c.ActorId,
                Key = c.Input.ClientRequestId,
                PayloadHash = payloadHash,
                AttendanceRecordId = attendance.Id,
                ResponseJson = JsonSerializer.Serialize(response)
            });
            if (!repeated)
                db.Audit.Add(new()
                {
                    ActorId = c.ActorId,
                    Action = "CheckIn",
                    EntityType = "CatechistAttendance",
                    EntityId = attendance.Id,
                    Detail = c.Input.Method
                });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return response;
        }
        if (!session.StudentAttendanceEnabled)
            throw new AppException(409, "STUDENT_ATTENDANCE_DISABLED", "Buổi này không điểm danh HS.");
        var hash = AuthService.Hash(c.Input.Credential.Trim());
        var credential = await db.Credentials.SingleOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAtUtc == null, ct) ?? throw new AppException(400, "CREDENTIAL_INVALID", "Thẻ không hợp lệ hoặc đã được thu hồi.");
        if (!await db.Rosters.AnyAsync(x => x.SessionId == session.Id && x.StudentId == credential.StudentId, ct))
            throw new AppException(409, "WRONG_CLASS", "Học sinh không thuộc danh sách buổi này.");
        var student = await db.Students.SingleAsync(x => x.Id == credential.StudentId, ct);
        var record = await db.Attendance.SingleOrDefaultAsync(x => x.SessionId == session.Id && x.StudentId == student.Id, ct);
        var duplicate = record != null;
        if (record == null)
        {
            record = new()
            {
                SessionId = session.Id,
                StudentId = student.Id,
                ActorId = c.ActorId,
                RecordedAtUtc = now,
                Method = c.Input.Method
            };
            db.Attendance.Add(record);
        }
        // Updating the session's concurrency token serializes check-in against schedule/session changes.
        session.Version = Guid.NewGuid();
        var result = new CheckInResult(
            duplicate ? "AlreadyCheckedIn" : "Created",
            record.Id,
            student.Id,
            ((student.SaintName ?? "") + " " + student.FullName).Trim(),
            student.Code,
            record.Present,
            record.RecordedAtUtc,
            record.Version);
        db.Idempotency.Add(new()
        {
            ActorId = c.ActorId,
            Key = c.Input.ClientRequestId,
            PayloadHash = payloadHash,
            AttendanceRecordId = record.Id,
            ResponseJson = JsonSerializer.Serialize(result)
        });
        if (!duplicate)
            db.Audit.Add(new()
            {
                ActorId = c.ActorId,
                Action = "CheckIn",
                EntityType = "Attendance",
                EntityId = record.Id
            });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }
    public async Task<object> Report(ReportQuery q, CancellationToken ct)
    {
        await access.Require(q.ActorId, Permission.ReportView, ct);
        if (q.From > q.Until || q.Until.DayNumber - q.From.DayNumber > 400)
            throw new AppException(400, "DATE_RANGE", "Khoảng báo cáo tối đa 400 ngày.");
        var ids = await access.ClassIds(q.ActorId, ct);
        if (q.ClassId != null)
        {
            await access.Class(q.ActorId, q.ClassId.Value, ct);
            ids = [q.ClassId.Value];
        }
        if (q.YearId != null)
            ids = await db.Classes.Where(x => ids.Contains(x.Id) && x.AcademicYearId == q.YearId).Select(x => x.Id).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var revision = await db.Audit.LongCountAsync(ct);
        var cacheKey = $"report:{q.ActorId}:{q.ClassId}:{q.YearId}:{q.From}:{q.Until}:{revision}:{now:yyyyMMddHHmm}";
        var cached = await cache.Get(cacheKey);
        if (cached != null)
            return cached.Value;
        var sessions = await db.Sessions.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId)
            && x.Date >= q.From
            && x.Date <= q.Until
            && !x.Cancelled
            && x.StudentAttendanceEnabled
            && x.ClosesAtUtc <= now)
            .ToListAsync(ct);
        var pendingDays = await db.Sessions.Where(x => ids.Contains(x.ClassRoomId)
            && x.Date >= q.From
            && x.Date <= q.Until
            && !x.Cancelled
            && x.StudentAttendanceEnabled
            && x.ClosesAtUtc > now)
            .Select(x => new { x.ClassRoomId, x.Date })
            .ToListAsync(ct);
        var sessionIds = sessions.Select(x => x.Id).ToList();
        var roster = await db.Rosters.AsNoTracking().Where(x => sessionIds.Contains(x.SessionId)).ToListAsync(ct);
        var records = await db.Attendance.AsNoTracking().Where(x => sessionIds.Contains(x.SessionId) && x.Present).ToListAsync(ct);
        var present = records.Select(x => (x.SessionId, x.StudentId)).ToHashSet();
        var classes = await db.Classes.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var studentIds = roster.Select(x => x.StudentId).Distinct().ToList();
        var enrollment = await db.Enrollments.AsNoTracking().Where(x => ids.Contains(x.ClassRoomId) && x.From <= q.Until && (x.Until == null || x.Until > q.From))
            .ToListAsync(ct);
        studentIds.AddRange(enrollment.Select(x => x.StudentId));
        var students = await db.Students.AsNoTracking().Where(x => studentIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var sessionById = sessions.ToDictionary(s => s.Id);
        var rosterByClass = roster.ToLookup(r => sessionById[r.SessionId].ClassRoomId);
        var rosterByStudent = roster.ToLookup(r => (sessionById[r.SessionId].ClassRoomId, r.StudentId));
        var enrollmentByClass = enrollment.ToLookup(e => e.ClassRoomId);
        var pendingSet = pendingDays.Select(x => (x.ClassRoomId, x.Date)).ToHashSet();
        var rows = new List<object>();
        foreach (var cls in classes)
            foreach (var id in rosterByClass[cls.Id].Select(r => r.StudentId).Concat(enrollmentByClass[cls.Id].Select(e => e.StudentId)).Distinct())
            {
                var eligible = rosterByStudent[(cls.Id, id)].ToList();
                var p = eligible.Count(r => present.Contains((r.SessionId, r.StudentId)));
                var st = students[id];
                var daily = eligible.GroupBy(r => sessionById[r.SessionId].Date).Select(g => new
                {
                    date = g.Key,
                    finalized = !pendingSet.Contains((cls.Id, g.Key)),
                    present = g.All(r => present.Contains((r.SessionId, r.StudentId))),
                    partial = g.Any(r => present.Contains((r.SessionId, r.StudentId)))
                    && !g.All(r => present.Contains((r.SessionId, r.StudentId))),
                    sessions = g.Select(r => new { r.SessionId, opensAtUtc = sessionById[r.SessionId].OpensAtUtc, closesAtUtc = sessionById[r.SessionId].ClosesAtUtc, present = present.Contains((r.SessionId, r.StudentId)) })
                })
                    .OrderBy(x => x.date)
                    .ToList();
                rows.Add(new
                {
                    studentId = id,
                    st.Code,
                    st.SaintName,
                    st.FullName,
                    st.DateOfBirth,
                    classId = cls.Id,
                    className = cls.Name,
                    expected = eligible.Count,
                    present = p,
                    absent = eligible.Count - p,
                    rate = AttendanceRules.Rate(p, eligible.Count),
                    expectedDays = daily.Count(x => x.finalized),
                    presentDays = daily.Count(x => x.finalized && x.present),
                    absentDays = daily.Count(x => x.finalized && !x.present),
                    partialDays = daily.Count(x => x.finalized && x.partial),
                    days = daily
                });
            }
        var summary = classes.Select(cls => { var ss = sessions.Where(s => s.ClassRoomId == cls.Id).Select(s => s.Id).ToHashSet(); var rr = rosterByClass[cls.Id].ToList(); var p = rr.Count(r => present.Contains((r.SessionId, r.StudentId))); return new { classId = cls.Id, cls.Name, cls.Code, sessions = ss.Count, expected = rr.Count, present = p, absent = rr.Count - p, rate = AttendanceRules.Rate(p, rr.Count) }; })
            .ToList();
        var totalExpected = summary.Sum(x => x.expected);
        var totalPresent = summary.Sum(x => x.present);
        var result = new
        {
            from = q.From,
            until = q.Until,
            asOf = now,
            totalExpected,
            totalPresent,
            totalAbsent = totalExpected - totalPresent,
            rate = AttendanceRules.Rate(totalPresent, totalExpected),
            classes = summary,
            students = rows
        };
        await cache.Set(cacheKey, result);
        return result;
    }
}
