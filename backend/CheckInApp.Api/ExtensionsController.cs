using System.Text.Json;
using CheckInApp.Api.Security;
using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1")]
public class ExtensionsController(
    AppDbContext db,
    AccessService access,
    ReadService read,
    ICatalogService catalog,
    PresenceService presence,
    TimeProvider clock) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpPut("attendance-sessions/{id:guid}/schedule"), AspNetRole(Permission.ScheduleManage)]
    public async Task<IActionResult> EditSchedule(
        Guid id,
        SessionScheduleInput input,
        [FromServices] ScheduleService schedules,
        CancellationToken ct)
    {
        await schedules.EditSession(Actor, id, input, access, ct);
        return NoContent();
    }
    [HttpGet("attendance-sessions/{id:guid}/catechists"), AspNetRole(Permission.AttendanceView)]
    public Task<object> Teachers(Guid id, CancellationToken ct) => presence.List(Actor, id, ct);
    [HttpPut("attendance-sessions/{id:guid}/times/{personId:guid}")]
    public async Task<IActionResult> Times(Guid id, Guid personId, PresenceInput input, CancellationToken ct, bool teacher = false)
    {
        await presence.Save(Actor, id, personId, teacher, input, ct);
        return NoContent();
    }
    static (Permission Import, Permission Export, string[] Headers) Config(string kind) => kind switch
    {
        "students" => (Permission.StudentImport, Permission.StudentExport, StudentExcel.Headers),
        "catechists" => (Permission.CatechistImport, Permission.CatechistExport, CatechistExcel.Headers),
        "classes" => (Permission.ClassImport, Permission.ClassExport, ["Mã lớp", "Tên lớp"]),
        "years" => (Permission.YearImport, Permission.YearExport, ["Năm học", "Ngày bắt đầu", "Ngày kết thúc", "Hiện hành"]),
        _ => throw new AppException(400, "KIND_INVALID", "Chỉ hỗ trợ học sinh, giáo lý viên và lớp học.")
    };
    [HttpGet("excel/{kind}")]
    public async Task<IActionResult> Export(string kind, Guid? classId, Guid? yearId, bool template = false, CancellationToken ct = default)
    {
        var config = Config(kind);
        if (template)
        {
            var rights = await access.Rights(Actor, ct);
            if ((rights.Mask & (long)(config.Import | config.Export)) == 0)
                throw new AppException(403, "FORBIDDEN", "Không có quyền tải mẫu.");
        }
        else
            await access.Require(Actor, config.Export, ct);
        if (kind == "students")
        {
            if (classId == null || classId == Guid.Empty)
                throw new AppException(400, "CLASS_REQUIRED", "Chọn lớp trước khi tải mẫu hoặc xuất Excel học sinh.");
            await access.Class(Actor, classId.Value, ct);
        }
        if (kind == "classes")
        {
            if (yearId == null || !await db.Years.AnyAsync(x => x.Id == yearId, ct))
                throw new AppException(400, "YEAR_REQUIRED", "Chọn năm học trước khi tải Excel lớp học.");
        }
        var rows = new List<string[]> { config.Headers };
        if (!template)
        {
            for (var page = 1; ; page++)
            {
                var result = JsonSerializer.SerializeToElement(
                    await read.List(Actor, kind, null, classId, yearId, page, 100, ct),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var items = result.GetProperty("items").EnumerateArray().ToList();
                foreach (var item in items)
                    if (kind == "students")
                        rows.Add([item.GetProperty("saintName").ToString(), item.GetProperty("familyName").ToString(), item.GetProperty("givenName").ToString(), DateOnly.Parse(item.GetProperty("dateOfBirth").GetString()!).ToString("dd/MM/yyyy")]);
                    else if (kind == "catechists")
                        rows.Add([item.GetProperty("saintName").ToString(), item.GetProperty("familyName").ToString(), item.GetProperty("givenName").ToString()]);
                    else if (kind == "years")
                        rows.Add([item.GetProperty("name").ToString(), item.GetProperty("startDate").ToString(), item.GetProperty("endDate").ToString(), item.GetProperty("isActive").GetBoolean() ? "1" : "0"]);
                    else if (kind == "classes")
                        rows.Add([item.GetProperty("code").ToString(), item.GetProperty("name").ToString()]);
                    else
                        rows.Add(config.Headers.Select(h => item.TryGetProperty(h, out var v) ? v.ToString() : "").ToArray());
                if (items.Count < 100)
                    break;
                if (rows.Count > 50000)
                    throw new AppException(400, "EXPORT_LIMIT", "Chọn phạm vi nhỏ hơn (tối đa 50.000 dòng).");
            }
        }
        return File(
            ExcelFile.Write(rows),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"CheckInApp-{kind}{(template ? "-template" : "")}.xlsx");
    }
    [HttpPost("excel/{kind}"), RequestSizeLimit(7_000_000)]
    public async Task<object> Import(string kind, ExcelUpload input, CancellationToken ct)
    {
        var config = Config(kind);
        await access.Require(Actor, config.Import, ct);
        await access.Require(Actor, kind switch
        {
            "students" => Permission.StudentManage,
            "classes" => Permission.ClassManage,
            "years" => Permission.YearManage,
            _ => Permission.CatechistManage
        }, ct);
        var today = AttendanceRules.Today(clock);
        if (kind == "students")
        {
            if (input.ClassId == null || input.ClassId == Guid.Empty)
                throw new AppException(400, "CLASS_REQUIRED", "Chọn lớp trước khi nhập Excel học sinh.");
            await access.Class(Actor, input.ClassId.Value, ct);
        }
        if (kind == "classes" && (input.YearId == null || !await db.Years.AnyAsync(x => x.Id == input.YearId, ct)))
            throw new AppException(400, "YEAR_REQUIRED", "Chọn năm học trước khi nhập lớp học.");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(input.Base64);
        }
        catch { throw new AppException(400, "EXCEL_INVALID", "File không hợp lệ."); }
        if (bytes.Length > 5_000_000)
            throw new AppException(400, "EXCEL_SIZE", "File tối đa 5 MB.");
        var rows = ExcelFile.Read(bytes);
        var results = new List<object>();
        for (int i = 0; i < rows.Count; i++)
        {
            try
            {
                var row = rows[i];
                if (!config.Headers.All(row.ContainsKey))
                    throw new AppException(400, "HEADERS", "Thiếu cột. Hãy tải file mẫu.");
                if (kind == "years")
                {
                    var start = DateOnly.ParseExact(row["Ngày bắt đầu"].Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                    var end = DateOnly.ParseExact(row["Ngày kết thúc"].Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                    var active = row["Hiện hành"].Trim();
                    if (active is not ("0" or "1"))
                        throw new AppException(400, "VALIDATION", "Hiện hành phải là 0 hoặc 1.");
                    row = new Dictionary<string, string> { { "id", "" }, { "name", row["Năm học"] }, { "startDate", start.ToString("yyyy-MM-dd") }, { "endDate", end.ToString("yyyy-MM-dd") }, { "isActive", active == "1" ? "true" : "false" } };
                }
                if (kind == "classes")
                    row = new Dictionary<string, string> { ["id"] = "", ["code"] = row["Mã lớp"], ["name"] = row["Tên lớp"], ["academicYearId"] = input.YearId!.Value.ToString() };
                if (kind == "students")
                    row = StudentExcel.Parse(row, input.ClassId!.Value);
                if (kind == "catechists")
                    row = CatechistExcel.Parse(row);
                Guid? id = string.IsNullOrWhiteSpace(row["id"])
                    ? null
                    : Guid.TryParse(row["id"], out var parsed) ? parsed : throw new AppException(400, "ID_INVALID", "ID không hợp lệ.");
                if (kind == "catechists")
                {
                    var rights = await access.Rights(Actor, ct);
                    var ids = await access.ClassIds(Actor, ct);
                    var candidates = await db.Catechists.Where(x => rights.Global || db.Assignments.Any(a => a.CatechistId == x.Id && ids.Contains(a.ClassRoomId)))
                        .Select(x => new { x.Id, x.FullName, x.SaintName })
                        .ToListAsync(ct);
                    var matching = candidates.Where(x => string.Equals(StudentNames.Clean(x.FullName), row["fullName"], StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                        StudentNames.Clean(x.SaintName ?? ""),
                        StudentNames.Clean(row["saintName"]),
                        StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (matching.Count > 1)
                        throw new AppException(
                            400,
                            "AMBIGUOUS_CATECHIST",
                            "Có nhiều giáo lý viên trùng họ tên và tên Thánh. Hãy đối soát hồ sơ trên màn hình Giáo lý viên.");
                    id = matching.Select(x => (Guid?)x.Id).SingleOrDefault();
                }
                if (kind == "students")
                {
                    var birth = DateOnly.Parse(row["dateOfBirth"]);
                    var candidates = await db.Students.Where(x => x.DateOfBirth == birth
                        && db.Enrollments.Any(e => e.StudentId == x.Id
                        && e.ClassRoomId == input.ClassId
                        && e.From <= today
                        && (e.Until == null || today < e.Until)))
                        .Select(x => new { x.Id, x.FullName })
                        .ToListAsync(ct);
                    var matching = candidates.Where(x => string.Equals(StudentNames.Clean(x.FullName), row["fullName"], StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (matching.Count > 1)
                        throw new AppException(
                            400,
                            "AMBIGUOUS_STUDENT",
                            "Có nhiều học sinh trùng họ tên và ngày sinh trong lớp. Hãy đối soát hồ sơ trên màn hình Học sinh.");
                    id = matching.Select(x => (Guid?)x.Id).SingleOrDefault();
                }
                if (id == null && kind == "students" && !string.IsNullOrWhiteSpace(row["code"]))
                    id = await db.Students.Where(x => x.Code == row["code"].Trim()).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
                        ?? throw new AppException(400, "CODE_INVALID", "Mã học sinh không tồn tại; dòng mới để trống mã.");
                if (id == null && kind == "classes" && Guid.TryParse(row["academicYearId"], out var yearId))
                    id = await db.Classes.Where(x => x.Code == row["code"].Trim().ToUpper() && x.AcademicYearId == yearId).Select(x => (Guid?)x.Id)
                        .SingleOrDefaultAsync(ct);
                if (kind == "years")
                    id = await db.Years.Where(x => x.Name == row["name"]).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
                if (id != null && kind == "students")
                {
                    if (!Guid.TryParse(row["classRoomId"], out var classId))
                        throw new AppException(400, "CLASS_INVALID", "Lớp không hợp lệ.");
                    await access.Class(Actor, classId, ct);
                    if (!await db.Enrollments.AnyAsync(
                        x => x.StudentId == id && x.ClassRoomId == classId && x.From <= today && (x.Until == null || today < x.Until),
                        ct))
                        throw new AppException(403, "CLASS_SCOPE", "Học sinh không thuộc lớp đã chọn.");
                }
                if (id != null && kind == "classes")
                    await access.Class(Actor, id.Value, ct);
                if (kind == "catechists" && id != null && !(await access.Rights(Actor, ct)).Global)
                {
                    var ids = await access.ClassIds(Actor, ct);
                    if (!await db.Assignments.AnyAsync(x => x.CatechistId == id && ids.Contains(x.ClassRoomId), ct))
                        throw new AppException(403, "CLASS_SCOPE", "Ngoài phạm vi phụ trách.");
                }
                if (id != null && !input.OverwriteExisting)
                {
                    var exists = kind switch
                    {
                        "students" => await db.Students.AnyAsync(x => x.Id == id, ct),
                        "years" => await db.Years.AnyAsync(x => x.Id == id, ct),
                        "classes" => await db.Classes.AnyAsync(x => x.Id == id, ct),
                        _ => await db.Catechists.AnyAsync(x => x.Id == id, ct)
                    };
                    if (!exists)
                        throw new AppException(404, "NOT_FOUND", "ID không tồn tại.");
                    results.Add(new
                    {
                        row = i + 2,
                        success = true,
                        skipped = true,
                        message = "Bỏ qua: dữ liệu đã có (chưa chọn chép đè)."
                    });
                    continue;
                }
                if (kind == "students"
                    && double.TryParse(
                    row["dateOfBirth"],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var serial))
                {
                    if (serial < 1 || serial > 100000)
                        throw new AppException(400, "DATE_INVALID", "Ngày sinh không hợp lệ.");
                    row["dateOfBirth"] = DateTime.FromOADate(serial).ToString("yyyy-MM-dd");
                }
                await catalog.Save(new(Actor, kind, id, JsonSerializer.SerializeToElement(row)), ct);
                results.Add(new
                {
                    row = i + 2,
                    success = true,
                    message = "Đã lưu"
                });
            }
            catch (AppException e) { results.Add(new { row = i + 2, success = false, message = e.Message }); }
            catch (FormatException) { results.Add(new { row = i + 2, success = false, message = "Ngày phải theo yyyy-MM-dd." }); }
            catch (DbUpdateException) { results.Add(new { row = i + 2, success = false, message = "Dữ liệu trùng hoặc tham chiếu không hợp lệ." }); }
            finally { db.ChangeTracker.Clear(); }
        }
        return new
        {
            results
        };
    }
}
public record ExcelUpload(string Base64, bool OverwriteExisting = false, Guid? ClassId = null, Guid? YearId = null);
