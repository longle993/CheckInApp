using CheckInApp.Application;
using CheckInApp.Domain;
namespace CheckInApp.Infrastructure;
public static class CatechistExcel
{
    public static readonly string[] Headers=["Tên Thánh","Họ và tên đệm","Tên"];
    public static Dictionary<string,string> Parse(Dictionary<string,string> row)
    {
        if(row.Count!=3 || !Headers.All(row.ContainsKey)) throw new AppException(400,"HEADERS","Mẫu giáo lý viên gồm: Tên Thánh, Họ và tên đệm, Tên. Hãy tải mẫu mới.");
        var family=StudentNames.Clean(row["Họ và tên đệm"]);var given=StudentNames.Clean(row["Tên"]);
        if(given.Length==0 || StudentNames.Full(family,given).Length>200) throw new AppException(400,"NAME_INVALID","Cần nhập Tên; họ tên đầy đủ tối đa 200 ký tự.");
        return new() { ["id"]="",["familyName"]=family,["givenName"]=given,["fullName"]=StudentNames.Full(family,given),["saintName"]=row["Tên Thánh"].Trim() };
    }
}
