using System.Globalization;
using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace CheckInApp.Infrastructure;

public class ExcelService(
    AppDbContext db,
    AccessService access,
    ReadService read,
    ICatalogService catalog,
    TimeProvider clock) : IExcelService
{
    static (Permission Import, Permission Export, string[] Headers) Config(string kind) => kind switch
    {
        "students" => (Permission.StudentImport, Permission.StudentExport, StudentExcel.Headers),
        "catechists" => (Permission.CatechistImport, Permission.CatechistExport, CatechistExcel.Headers),
        "classes" => (Permission.ClassImport, Permission.ClassExport, ["Mã lớp", "Tên lớp"]),
        "years" => (Permission.YearImport, Permission.YearExport, ["Năm học", "Ngày bắt đầu", "Ngày kết thúc", "Hiện hành"]),
        _ => throw new AppException(400, "KIND_INVALID", "Chỉ hỗ trợ học sinh, giáo lý viên và lớp học.")
    };

    public async Task<ExportedFile> Export(
        Guid actorId,
        string kind,
        Guid? classId,
        Guid? yearId,
        bool template,
        CancellationToken ct)
    {
        var config = Config(kind);
        if (template)
        {
            var rights = await access.Rights(actorId, ct);
            if ((rights.Mask & (long)(config.Import | config.Export)) == 0)
                throw new AppException(403, "FORBIDDEN", "Không có quyền tải mẫu.");
        }
        else
        {
            await access.Require(actorId, config.Export, ct);
        }
        if (kind == "students")
        {
            if (classId == null || classId == Guid.Empty)
                throw new AppException(400, "CLASS_REQUIRED", "Chọn lớp trước khi tải mẫu hoặc xuất Excel học sinh.");
            await access.Class(actorId, classId.Value, ct);
        }
        if (kind == "classes" && (yearId == null || !await db.Years.AnyAsync(x => x.Id == yearId, ct)))
            throw new AppException(400, "YEAR_REQUIRED", "Chọn năm học trước khi tải Excel lớp học.");
        var rows = new List<string[]> { config.Headers };
        if (!template)
        {
            for (var page = 1; ; page++)
            {
                var result = JsonSerializer.SerializeToElement(
                    await read.List(actorId, kind, null, classId, yearId, page, 100, ct),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var items = result.GetProperty("items").EnumerateArray().ToList();
                foreach (var item in items)
                {
                    if (kind == "students")
                        rows.Add([
                            item.GetProperty("saintName").ToString(),
                            item.GetProperty("familyName").ToString(),
                            item.GetProperty("givenName").ToString(),
                            DateOnly.Parse(item.GetProperty("dateOfBirth").GetString()!).ToString("dd/MM/yyyy")]);
                    else if (kind == "catechists")
                        rows.Add([item.GetProperty("saintName").ToString(), item.GetProperty("familyName").ToString(), item.GetProperty("givenName").ToString()]);
                    else if (kind == "years")
                        rows.Add([
                            item.GetProperty("name").ToString(),
                            item.GetProperty("startDate").ToString(),
                            item.GetProperty("endDate").ToString(),
                            item.GetProperty("isActive").GetBoolean() ? "1" : "0"]);
                    else if (kind == "classes")
                        rows.Add([item.GetProperty("code").ToString(), item.GetProperty("name").ToString()]);
                    else
                        rows.Add(config.Headers.Select(header => item.TryGetProperty(header, out var value) ? value.ToString() : "").ToArray());
                }
                if (items.Count < 100)
                    break;
                if (rows.Count > 50000)
                    throw new AppException(400, "EXPORT_LIMIT", "Chọn phạm vi nhỏ hơn (tối đa 50.000 dòng).");
            }
        }
        return new(
            ExcelFile.Write(rows),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"CheckInApp-{kind}{(template ? "-template" : "")}.xlsx");
    }

    public async Task<object> Import(Guid actorId, string kind, ExcelUpload input, CancellationToken ct)
    {
        var config = Config(kind);
        await access.Require(actorId, config.Import, ct);
        await access.Require(actorId, kind switch
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
            await access.Class(actorId, input.ClassId.Value, ct);
        }
        if (kind == "classes" && (input.YearId == null || !await db.Years.AnyAsync(x => x.Id == input.YearId, ct)))
            throw new AppException(400, "YEAR_REQUIRED", "Chọn năm học trước khi nhập lớp học.");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(input.Base64);
        }
        catch
        {
            throw new AppException(400, "EXCEL_INVALID", "File không hợp lệ.");
        }
        if (bytes.Length > 5_000_000)
            throw new AppException(400, "EXCEL_SIZE", "File tối đa 5 MB.");
        var rows = ExcelFile.Read(bytes);
        var results = new List<object>();
        for (var index = 0; index < rows.Count; index++)
        {
            try
            {
                var row = rows[index];
                if (!config.Headers.All(row.ContainsKey))
                    throw new AppException(400, "HEADERS", "Thiếu cột. Hãy tải file mẫu.");
                if (kind == "years")
                {
                    var start = DateOnly.ParseExact(row["Ngày bắt đầu"].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var end = DateOnly.ParseExact(row["Ngày kết thúc"].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var active = row["Hiện hành"].Trim();
                    if (active is not ("0" or "1"))
                        throw new AppException(400, "VALIDATION", "Hiện hành phải là 0 hoặc 1.");
                    row = new()
                    {
                        ["id"] = "",
                        ["name"] = row["Năm học"],
                        ["startDate"] = start.ToString("yyyy-MM-dd"),
                        ["endDate"] = end.ToString("yyyy-MM-dd"),
                        ["isActive"] = active == "1" ? "true" : "false"
                    };
                }
                if (kind == "classes")
                    row = new()
                    {
                        ["id"] = "",
                        ["code"] = row["Mã lớp"],
                        ["name"] = row["Tên lớp"],
                        ["academicYearId"] = input.YearId!.Value.ToString()
                    };
                if (kind == "students")
                    row = StudentExcel.Parse(row, input.ClassId!.Value);
                if (kind == "catechists")
                    row = CatechistExcel.Parse(row);
                Guid? id = string.IsNullOrWhiteSpace(row["id"])
                    ? null
                    : Guid.TryParse(row["id"], out var parsed) ? parsed : throw new AppException(400, "ID_INVALID", "ID không hợp lệ.");
                if (kind == "catechists")
                {
                    var rights = await access.Rights(actorId, ct);
                    var classIds = await access.ClassIds(actorId, ct);
                    var candidates = await db.Catechists
                        .WhereIf(!rights.Global, x => db.Assignments.Any(a => a.CatechistId == x.Id && classIds.Contains(a.ClassRoomId)))
                        .Select(x => new { x.Id, x.FullName, x.SaintName })
                        .ToListAsync(ct);
                    var matching = candidates.Where(x => string.Equals(StudentNames.Clean(x.FullName), row["fullName"], StringComparison.OrdinalIgnoreCase)
                        && string.Equals(StudentNames.Clean(x.SaintName ?? ""), StudentNames.Clean(row["saintName"]), StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matching.Count > 1)
                        throw new AppException(400, "AMBIGUOUS_CATECHIST", "Có nhiều giáo lý viên trùng họ tên và tên Thánh. Hãy đối soát hồ sơ trên màn hình Giáo lý viên.");
                    id = matching.Select(x => (Guid?)x.Id).SingleOrDefault();
                }
                if (kind == "students")
                {
                    var birth = DateOnly.Parse(row["dateOfBirth"]);
                    var candidates = await db.Students
                        .Where(x => x.DateOfBirth == birth
                            && db.Enrollments.Any(enrollment => enrollment.StudentId == x.Id
                                && enrollment.ClassRoomId == input.ClassId
                                && enrollment.From <= today
                                && (enrollment.Until == null || today < enrollment.Until)))
                        .Select(x => new { x.Id, x.FullName })
                        .ToListAsync(ct);
                    var matching = candidates.Where(x => string.Equals(StudentNames.Clean(x.FullName), row["fullName"], StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matching.Count > 1)
                        throw new AppException(400, "AMBIGUOUS_STUDENT", "Có nhiều học sinh trùng họ tên và ngày sinh trong lớp. Hãy đối soát hồ sơ trên màn hình Học sinh.");
                    id = matching.Select(x => (Guid?)x.Id).SingleOrDefault();
                }
                if (id == null && kind == "students" && !string.IsNullOrWhiteSpace(row["code"]))
                    id = await db.Students.Where(x => x.Code == row["code"].Trim()).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
                        ?? throw new AppException(400, "CODE_INVALID", "Mã học sinh không tồn tại; dòng mới để trống mã.");
                if (id == null && kind == "classes" && Guid.TryParse(row["academicYearId"], out var yearId))
                    id = await db.Classes.Where(x => x.Code == row["code"].Trim().ToUpper() && x.AcademicYearId == yearId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
                if (kind == "years")
                    id = await db.Years.Where(x => x.Name == row["name"]).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
                if (id != null && kind == "students")
                {
                    if (!Guid.TryParse(row["classRoomId"], out var classId))
                        throw new AppException(400, "CLASS_INVALID", "Lớp không hợp lệ.");
                    await access.Class(actorId, classId, ct);
                    if (!await db.Enrollments.AnyAsync(
                        x => x.StudentId == id && x.ClassRoomId == classId && x.From <= today && (x.Until == null || today < x.Until),
                        ct))
                        throw new AppException(403, "CLASS_SCOPE", "Học sinh không thuộc lớp đã chọn.");
                }
                if (id != null && kind == "classes")
                    await access.Class(actorId, id.Value, ct);
                if (kind == "catechists" && id != null && !(await access.Rights(actorId, ct)).Global)
                {
                    var classIds = await access.ClassIds(actorId, ct);
                    if (!await db.Assignments.AnyAsync(x => x.CatechistId == id && classIds.Contains(x.ClassRoomId), ct))
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
                        row = index + 2,
                        success = true,
                        skipped = true,
                        message = "Bỏ qua: dữ liệu đã có (chưa chọn chép đè)."
                    });
                    continue;
                }
                if (kind == "students" && double.TryParse(row["dateOfBirth"], NumberStyles.Number, CultureInfo.InvariantCulture, out var serial))
                {
                    if (serial < 1 || serial > 100000)
                        throw new AppException(400, "DATE_INVALID", "Ngày sinh không hợp lệ.");
                    row["dateOfBirth"] = DateTime.FromOADate(serial).ToString("yyyy-MM-dd");
                }
                await catalog.Save(new(actorId, kind, id, JsonSerializer.SerializeToElement(row)), ct);
                results.Add(new
                {
                    row = index + 2,
                    success = true,
                    message = "Đã lưu"
                });
            }
            catch (AppException exception)
            {
                results.Add(new
                {
                    row = index + 2,
                    success = false,
                    message = exception.Message
                });
            }
            catch (FormatException)
            {
                results.Add(new
                {
                    row = index + 2,
                    success = false,
                    message = "Ngày phải theo yyyy-MM-dd."
                });
            }
            catch (DbUpdateException)
            {
                results.Add(new
                {
                    row = index + 2,
                    success = false,
                    message = "Dữ liệu trùng hoặc tham chiếu không hợp lệ."
                });
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
        return new
        {
            results
        };
    }
}
