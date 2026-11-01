using System.Globalization;
using System.Text;
using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public class CatalogService(AppDbContext db, AccessService access, ScheduleService schedules, TimeProvider clock) : ICatalogService
{
    static string Text(JsonElement d, string k, bool required = true)
    {
        if (d.TryGetProperty(k, out var p) && p.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            throw Invalid($"{k} phải là chuỗi.");
        var v = p.ValueKind == JsonValueKind.String ? p.GetString()?.Trim() : null;
        if (required && string.IsNullOrWhiteSpace(v))
            throw new AppException(400, "VALIDATION", $"Thiếu thông tin {k}.");
        if (v?.Length > 200)
            throw new AppException(400, "VALIDATION", $"{k} quá dài.");
        return v ?? "";
    }
    static bool Flag(JsonElement d, string key, bool defaultValue = false)
    {
        if (!d.TryGetProperty(key, out var p))
            return defaultValue;
        if (p.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw Invalid($"{key} phải là true hoặc false.");
        return p.GetBoolean();
    }
    static Guid Id(JsonElement d, string k) => Guid.TryParse(Text(d, k), out var id) ? id : throw new AppException(400, "VALIDATION", $"{k} không hợp lệ.");
    static DateOnly Date(JsonElement d, string k) => DateOnly.TryParseExact(Text(d, k), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw new AppException(400, "VALIDATION", $"{k} phải là yyyy-MM-dd.");
    public static string Search(string text) => string.Concat(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Replace('đ', 'd').Replace('Đ', 'D').ToUpperInvariant();
    public async Task<object> Save(CatalogCommand c, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var d = c.Data;
        if (d.ValueKind != JsonValueKind.Object)
            throw Invalid("Nội dung phải là một đối tượng JSON.");
        var today = AttendanceRules.Today(clock);
        Entity entity;
        Guid? sync = null;
        switch (c.Kind)
        {
            case "users":
                await access.Require(c.ActorId, Permission.UserCreate, ct);
                if (c.Id != null)
                    throw Invalid("Dùng luồng duyệt hoặc khóa tài khoản để đổi trạng thái.");
                var email = Text(d, "email").ToLowerInvariant();
                if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
                    throw Invalid("Email không hợp lệ.");
                var password = Text(d, "password");
                AuthService.ValidatePassword(password);
                var user = new AppUser { Email = email, FullName = Text(d, "fullName"), Status = AccountStatus.PendingApproval };
                user.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>().HashPassword(user, password);
                db.Users.Add(user);
                entity = user;
                break;
            case "years":
                await access.Require(c.ActorId, Permission.YearManage, ct);
                var y = c.Id == null ? new AcademicYear() : await db.Years.FindAsync([c.Id], ct) ?? throw Missing();
                y.Name = Text(d, "name");
                y.StartDate = Date(d, "startDate");
                y.EndDate = Date(d, "endDate");
                if (y.EndDate < y.StartDate || y.EndDate.DayNumber - y.StartDate.DayNumber > 400)
                    throw Invalid("Khoảng năm học không hợp lệ.");
                if(db.Database.IsSqlServer()) await db.Years.FromSqlRaw("SELECT * FROM [Years] WITH (UPDLOCK,HOLDLOCK)").LoadAsync(ct);
                var overlap=await db.Years.FirstOrDefaultAsync(x=>x.Id!=y.Id && x.StartDate<=y.EndDate && y.StartDate<=x.EndDate,ct);
                if(overlap!=null) throw new AppException(409,"YEAR_OVERLAP",$"Năm học trùng khoảng với {overlap.Name}: {overlap.StartDate:dd/MM/yyyy}–{overlap.EndDate:dd/MM/yyyy}.");
                var affectedClasses=db.Classes.Where(x=>x.AcademicYearId==y.Id).Select(x=>x.Id);
                if(await db.Sessions.AnyAsync(x=>affectedClasses.Contains(x.ClassRoomId)&&(x.Date<y.StartDate||x.Date>y.EndDate),ct)||await db.Schedules.AnyAsync(x=>affectedClasses.Contains(x.ClassRoomId)&&(x.From<y.StartDate||x.Until>y.EndDate),ct)) throw Invalid("Khoảng năm học mới không bao phủ lịch/buổi hiện có.");
                y.IsActive = Flag(d, "isActive");
                if (y.IsActive)
                foreach (var other in await db.Years.Where(x => x.Id != y.Id && x.IsActive).ToListAsync(ct))
                    other.IsActive = false;
                if (c.Id == null)
                    db.Years.Add(y);
                entity = y;
                break;
            case "classes":
                await access.Require(c.ActorId, Permission.ClassManage, ct);
                var cl = c.Id == null ? new ClassRoom() : await db.Classes.FindAsync([c.Id], ct) ?? throw Missing();
                if (c.Id != null)
                    await access.Class(c.ActorId, cl.Id, ct);
                var yearId = Id(d, "academicYearId");
                if (c.Id != null && cl.AcademicYearId != yearId)
                    throw Invalid("Không đổi năm học của lớp đã tạo.");
                if (!await db.Years.AnyAsync(x => x.Id == yearId && x.EndDate >= today, ct))
                    throw Invalid("Năm học đã kết thúc hoặc không tồn tại.");
                cl.AcademicYearId = yearId;
                cl.Code = Text(d, "code").ToUpperInvariant();
                cl.Name = Text(d, "name");
                if (c.Id == null)
                    db.Classes.Add(cl);
                entity = cl;
                break;
            case "students":
                await access.Require(c.ActorId, Permission.StudentManage, ct);
                var classId = Id(d, "classRoomId");
                await access.Class(c.ActorId, classId, ct);
                var student = c.Id == null ? new Student() : await db.Students.FindAsync([c.Id], ct) ?? throw Missing();
                if (c.Id != null && !await db.Enrollments.AnyAsync(x => x.StudentId == student.Id && x.ClassRoomId == classId && x.From <= today && (x.Until == null || today < x.Until), ct))
                    throw new AppException(403, "CLASS_SCOPE", "Học sinh không thuộc lớp đã chọn.");
                if (d.TryGetProperty("givenName", out _) || d.TryGetProperty("familyName", out _))
                {
                    student.FamilyName = StudentNames.Clean(Text(d, "familyName", false));
                    student.GivenName = StudentNames.Clean(Text(d, "givenName"));
                }
                else
                {
                    (student.FamilyName, student.GivenName) = StudentNames.Split(Text(d, "fullName"));
                }
                student.FullName = StudentNames.Full(student.FamilyName, student.GivenName);
                if (student.FullName.Length > 200) throw Invalid("Họ tên đầy đủ tối đa 200 ký tự.");
                student.SaintName = Text(d, "saintName", false);
                student.DateOfBirth = Date(d, "dateOfBirth");
                if (student.DateOfBirth > today || student.DateOfBirth < today.AddYears(-120))
                    throw Invalid("Ngày sinh không hợp lệ.");
                student.SearchName = Search(student.FullName + " " + student.SaintName);
                if (c.Id == null)
                {
                    student.Code = "HS" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
                    db.Students.Add(student);
                    var from = d.TryGetProperty("from", out _) ? Date(d, "from") : today;
                    if (from < today)
                        throw Invalid("Ghi danh hồi tố cần quy trình đối soát, không được tự đổi lịch sử.");
                    var yr = await (from cc in db.Classes join yy in db.Years on cc.AcademicYearId equals yy.Id where cc.Id == classId select yy).SingleAsync(ct);
                    if (from < yr.StartDate || from > yr.EndDate)
                        throw Invalid("Ngày ghi danh phải trong năm học.");
                    db.Enrollments.Add(new()
                    {
                        StudentId = student.Id,
                        ClassRoomId = classId,
                        From = from
                    });
                    sync = classId;
                }
                entity = student;
                break;
            case "transfers":
                await access.Require(c.ActorId, Permission.StudentManage, ct);
                var enrollment = await db.Enrollments.FindAsync([Id(d, "enrollmentId")], ct) ?? throw Missing();
                var to = Id(d, "classRoomId");
                await access.Class(c.ActorId, enrollment.ClassRoomId, ct);
                await access.Class(c.ActorId, to, ct);
                var effective = Date(d, "from");
                if (effective < today || effective <= enrollment.From || enrollment.Until != null)
                    throw Invalid("Ngày chuyển phải sau ngày ghi danh, không hồi tố.");
                var source = await db.Classes.SingleAsync(x => x.Id == enrollment.ClassRoomId, ct);
                var dest = await db.Classes.SingleAsync(x => x.Id == to, ct);
                if (source.AcademicYearId != dest.AcademicYearId || source.Id == dest.Id)
                    throw Invalid("Chọn lớp khác trong cùng năm học.");
                var destYear = await db.Years.SingleAsync(x => x.Id == dest.AcademicYearId, ct);
                if (effective > destYear.EndDate)
                    throw Invalid("Ngày chuyển ngoài năm học.");
                var sourceFuture = await db.Sessions.AnyAsync(x => x.ClassRoomId == source.Id && x.Date == effective && x.OpensAtUtc <= clock.GetUtcNow().UtcDateTime, ct);
                if (sourceFuture)
                    throw Invalid("Buổi của ngày chuyển đã bắt đầu. Chọn ngày tiếp theo.");
                enrollment.Until = effective;
                var newEnrollment = new Enrollment { StudentId = enrollment.StudentId, ClassRoomId = to, From = effective };
                db.Enrollments.Add(newEnrollment);
                await db.SaveChangesAsync(ct);
                await schedules.Synchronize(source.Id, ct);
                sync = to;
                entity = newEnrollment;
                break;
            case "catechists":
                await access.Require(c.ActorId, Permission.CatechistManage, ct);
                var ca = c.Id == null ? new Catechist() : await db.Catechists.FindAsync([c.Id], ct) ?? throw Missing();
                if (d.TryGetProperty("givenName", out _) || d.TryGetProperty("familyName", out _)) {
                    ca.FamilyName=StudentNames.Clean(Text(d,"familyName",false));
                    ca.GivenName=StudentNames.Clean(Text(d,"givenName"));
                } else (ca.FamilyName,ca.GivenName)=StudentNames.Split(Text(d,"fullName"));
                ca.FullName=StudentNames.Full(ca.FamilyName,ca.GivenName);
                if(ca.FullName.Length>200) throw Invalid("Họ tên đầy đủ tối đa 200 ký tự.");
                ca.SaintName = Text(d, "saintName", false);
                if (c.Id == null)
                    db.Catechists.Add(ca);
                entity = ca;
                break;
            case "assignments":
                await access.Require(c.ActorId, Permission.AssignmentManage, ct);
                var assignment = new ClassAssignment { ClassRoomId = Id(d, "classRoomId"), CatechistId = Id(d, "catechistId"), From = Date(d, "from") };
                await access.Class(c.ActorId, assignment.ClassRoomId, ct);
                if (!await db.Catechists.AnyAsync(x => x.Id == assignment.CatechistId, ct))
                    throw Missing();
                if (await db.Assignments.AnyAsync(x => x.ClassRoomId == assignment.ClassRoomId && x.CatechistId == assignment.CatechistId && x.Until == null, ct))
                    throw Invalid("Giáo lý viên đã được phân công.");
                db.Assignments.Add(assignment);
                entity = assignment;
                break;
            case "schedules":
                await access.Require(c.ActorId, Permission.ScheduleManage, ct);
                var sid = Id(d, "classRoomId");
                await access.Class(c.ActorId, sid, ct);
                await schedules.LockClass(sid,ct);
                var sc = c.Id==null?null:await db.Schedules.SingleOrDefaultAsync(x => x.Id==c.Id && x.ClassRoomId == sid, ct);
                if(c.Id!=null&&sc==null)throw Missing();
                var isNew = sc == null;
                sc ??= new()
                {
                    ClassRoomId = sid
                };
                sc.From = Date(d, "from");
                sc.Until = Date(d, "until");
                sc.DaysMask = d.TryGetProperty("daysMask", out var maskElement) && maskElement.ValueKind == JsonValueKind.Number && maskElement.TryGetInt32(out var days) ? days : throw Invalid("Chọn các thứ hợp lệ.");
                if (!TimeOnly.TryParse(Text(d, "opensAt"), out var open) || !TimeOnly.TryParse(Text(d, "closesAt"), out var close) || open >= close)
                    throw Invalid("Giờ đóng phải sau giờ mở trong cùng ngày.");
                sc.StudentAttendanceEnabled = Flag(d, "studentAttendanceEnabled", true);
                sc.CatechistAttendanceEnabled = Flag(d, "catechistAttendanceEnabled", true);
                if (!sc.StudentAttendanceEnabled && !sc.CatechistAttendanceEnabled) throw Invalid("Chọn ít nhất HS hoặc GLV.");
                sc.OpensAt = open;
                sc.ClosesAt = close;
                if (sc.From > sc.Until || sc.DaysMask < 1 || sc.DaysMask > 127)
                    throw Invalid("Chọn ngày và khoảng lịch hợp lệ.");
                var sy = await (from cc in db.Classes join yy in db.Years on cc.AcademicYearId equals yy.Id where cc.Id == sid select yy).SingleAsync(ct);
                if (sc.From < sy.StartDate || sc.Until > sy.EndDate)
                    throw Invalid("Lịch phải trong năm học.");
                sc.Version++;
                if (isNew)
                    db.Schedules.Add(sc);
                sync = sid;
                entity = sc;
                break;
            case "exceptions":
                await access.Require(c.ActorId, Permission.ScheduleManage, ct);
                var eid = Id(d, "classRoomId");
                await access.Class(c.ActorId, eid, ct);
                var edate = Date(d, "date");
                var exceptionScheduleId=d.TryGetProperty("scheduleId",out var sp)&&Guid.TryParse(sp.GetString(),out var parsedSchedule)?(Guid?)parsedSchedule:null;
                await schedules.LockClass(eid,ct);
                var exists = await db.Sessions.Where(x => x.ClassRoomId == eid && (x.SourceDate??x.Date) == edate && (exceptionScheduleId==null||x.ScheduleId==exceptionScheduleId)).OrderBy(x=>x.OpensAtUtc).FirstOrDefaultAsync(ct);
                if (edate < today || (exists != null && exists.OpensAtUtc <= clock.GetUtcNow().UtcDateTime))
                    throw Invalid("Không thay ngoại lệ cho buổi đã bắt đầu hoặc quá khứ.");
                var sch = await db.Schedules.Where(x => x.ClassRoomId == eid && (exceptionScheduleId==null||x.Id==exceptionScheduleId)).OrderBy(x=>x.OpensAt).FirstOrDefaultAsync(ct) ?? throw Invalid("Cần cấu hình lịch trước.");
                if (edate < sch.From || edate > sch.Until)
                    throw Invalid("Ngày ngoại lệ ngoài khoảng lịch.");
                var ex = await db.Exceptions.SingleOrDefaultAsync(x => x.ClassRoomId == eid && x.ScheduleId==sch.Id && x.Date == edate, ct);
                if (ex == null)
                {
                    ex = new()
                    {
                        ClassRoomId = eid,
                        ScheduleId=sch.Id,
                        Date = edate
                    };
                    db.Exceptions.Add(ex);
                }
                ex.Cancelled = Flag(d, "cancelled");
                ex.StudentAttendanceEnabled = Flag(d, "studentAttendanceEnabled", sch.StudentAttendanceEnabled);
                ex.CatechistAttendanceEnabled = Flag(d, "catechistAttendanceEnabled", sch.CatechistAttendanceEnabled);
                if (!ex.Cancelled && !ex.StudentAttendanceEnabled && !ex.CatechistAttendanceEnabled) throw Invalid("Chọn ít nhất HS hoặc GLV.");
                ex.Reason = Text(d, "reason");
                ex.OpensAt = sch.OpensAt;
                ex.ClosesAt = sch.ClosesAt;
                sync = eid;
                entity = ex;
                break;
            case "roles":
                await access.Require(c.ActorId, Permission.RoleManage, ct);
                var role = c.Id == null ? new AppRole() : await db.Roles.FindAsync([c.Id], ct) ?? throw Missing();
                if (role.Name == "Admin")
                    throw Invalid("Vai trò Admin hệ thống không được sửa.");
                if (!long.TryParse(Text(d, "permissions"), out var mask) || mask < 0 || (mask & ~AllPermissions) != 0)
                    throw Invalid("Mask quyền không hợp lệ.");
                var caller = await access.Rights(c.ActorId, ct);
                if ((mask & caller.Mask) != mask)
                    throw new AppException(403, "GRANT_SCOPE", "Không được cấp quyền mình không có.");
                role.Name = Text(d, "name");
                role.Permissions = mask;
                role.GlobalScope = Flag(d, "globalScope");
                if (role.GlobalScope && !caller.Global)
                    throw new AppException(403, "GRANT_SCOPE", "Không được cấp phạm vi toàn đơn vị.");
                if (c.Id == null)
                    db.Roles.Add(role);
                entity = role;
                break;
            default:
                throw Invalid("Loại dữ liệu không được hỗ trợ.");
        }
        db.Audit.Add(new()
        {
            ActorId = c.ActorId,
            Action = c.Id == null ? "Create" : "Update",
            EntityType = c.Kind,
            EntityId = entity.Id
        });
        await db.SaveChangesAsync(ct);
        if (sync != null)
            await schedules.Synchronize(sync.Value, ct);
        await tx.CommitAsync(ct);
        if (entity is AppUser account)
            return new
            {
                account.Id,
                account.FullName,
                account.Email,
                status = account.Status.ToString()
            };
        if (entity is AppRole savedRole)
            return new
            {
                savedRole.Id,
                savedRole.Name,
                permissions = savedRole.Permissions.ToString(),
                savedRole.GlobalScope
            };
        return entity;
    }
    public static long AllPermissions => Enum.GetValues<Permission>().Aggregate(0L, (a, p) => a | (long)p);
    static AppException Invalid(string message) => new(400, "VALIDATION", message); static AppException Missing() => new(404, "NOT_FOUND", "Không tìm thấy dữ liệu.");
}
