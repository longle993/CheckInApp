using System.Globalization;
using CheckInApp.Application;
using CheckInApp.Domain;
namespace CheckInApp.Infrastructure;

public static class StudentExcel
{
    public static readonly string[] Headers = ["Tên Thánh", "Họ và tên đệm", "Tên", "Ngày sinh"];
    public static Dictionary<string, string> Parse(Dictionary<string, string> row, Guid classId)
    {
        if (row.Count != Headers.Length || !Headers.All(row.ContainsKey))
            throw new AppException(400, "HEADERS", "Mẫu học sinh chỉ gồm: Tên Thánh, Họ và tên đệm, Tên, Ngày sinh. Hãy tải mẫu mới.");
        var family = StudentNames.Clean(row["Họ và tên đệm"]);
        var given = StudentNames.Clean(row["Tên"]);
        if (given.Length == 0 || StudentNames.Full(family, given).Length > 200)
            throw new AppException(400, "NAME_INVALID", "Cần nhập Tên; họ tên đầy đủ tối đa 200 ký tự.");
        var text = row["Ngày sinh"].Trim();
        DateOnly date;
        if (double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial) && serial >= 1 && serial <= 100000)
            date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
        else if (!DateOnly.TryParseExact(
            text,
            ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date))
            throw new AppException(400, "DATE_INVALID", "Ngày sinh phải là dd/MM/yyyy, yyyy-MM-dd hoặc ngày Excel hợp lệ.");
        return new()
        {
            ["id"] = "",
            ["code"] = "",
            ["familyName"] = family,
            ["givenName"] = given,
            ["fullName"] = StudentNames.Full(family, given),
            ["saintName"] = row["Tên Thánh"].Trim(),
            ["dateOfBirth"] = date.ToString("yyyy-MM-dd"),
            ["classRoomId"] = classId.ToString()
        };
    }
}
