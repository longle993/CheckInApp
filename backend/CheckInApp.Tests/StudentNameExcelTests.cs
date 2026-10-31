using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;
public class StudentNameExcelTests
{
    [Theory]
    [InlineData("Nguyễn Văn An","Nguyễn Văn","An")]
    [InlineData("  Trần   Ngọc  Ánh ","Trần Ngọc","Ánh")]
    [InlineData("An","","An")]
    public void SplitNamesAndDisplayFullName(string text,string family,string given)
    { var parts=StudentNames.Split(text);Assert.Equal(family,parts.FamilyName);Assert.Equal(given,parts.GivenName);Assert.Equal(StudentNames.Clean(text),StudentNames.Full(family,given)); }
    [Theory]
    [InlineData("01/02/2016","2016-02-01")]
    [InlineData("1/2/2016","2016-02-01")]
    [InlineData("2016-02-01","2016-02-01")]
    [InlineData("42401","2016-02-01")]
    public void VietnameseTemplateUsesOnlyFourFieldsAndImplicitClass(string date,string expected)
    {
        var id=Guid.NewGuid();var row=StudentExcel.Parse(new() { ["Tên Thánh"]="Maria",["Họ và tên đệm"]="Nguyễn Văn",["Tên"]="An",["Ngày sinh"]=date },id);
        Assert.Equal("Nguyễn Văn An",row["fullName"]);Assert.Equal(expected,row["dateOfBirth"]);Assert.Equal(id.ToString(),row["classRoomId"]);Assert.Equal("",row["id"]);
    }
    [Fact]
    public void RejectExtraClassColumnAndInvalidDate()
    {
        var row=new Dictionary<string,string> { ["Tên Thánh"]="",["Họ và tên đệm"]="Nguyễn",["Tên"]="An",["Ngày sinh"]="31/02/2016" };
        Assert.Throws<AppException>(()=>StudentExcel.Parse(row,Guid.NewGuid()));row["Ngày sinh"]="01/02/2016";row["Mã lớp"]="OTHER";
        Assert.Throws<AppException>(()=>StudentExcel.Parse(row,Guid.NewGuid()));
    }
    [Fact]
    public async Task LegacyAndSplitWritesKeepFullNameInSync()
    {
        await using var f=new Fixture();await f.Init();var s=await f.Db.Students.SingleAsync();
        Assert.Equal("Nguyễn Gia",s.FamilyName);Assert.Equal("Bảo",s.GivenName);
        s.GivenName="An";await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();s=await f.Db.Students.SingleAsync();Assert.Equal("Nguyễn Gia An",s.FullName);
    }
}
