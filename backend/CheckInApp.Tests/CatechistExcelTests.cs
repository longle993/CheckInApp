using CheckInApp.Application;
using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;

public class CatechistExcelTests
{
    [Fact]
    public void ThreeVietnameseColumnsProduceSplitAndFullNames()
    {
        var row = CatechistExcel.Parse(new()
        {
            ["Tên Thánh"] = "Giuse",
            ["Họ và tên đệm"] = " Trần   Văn ",
            ["Tên"] = " An "
        });
        Assert.Equal("Trần Văn", row["familyName"]);
        Assert.Equal("An", row["givenName"]);
        Assert.Equal("Trần Văn An", row["fullName"]);
        Assert.Equal("", row["id"]);
    }
    [Fact]
    public void RejectMissingGivenName()
    {
        Assert.Throws<AppException>(() => CatechistExcel.Parse(new() { ["Tên Thánh"] = "", ["Họ và tên đệm"] = "Nguyễn", ["Tên"] = "" }));
    }
    [Fact]
    public async Task SplitTeacherNameKeepsUserLinkAndFullName()
    {
        await using var f = new Fixture();
        await f.Init();
        var teacher = await f.Db.Catechists.SingleAsync();
        Assert.Equal("GLV", teacher.GivenName);
        teacher.FamilyName = "Trần Văn";
        teacher.GivenName = "An";
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        teacher = await f.Db.Catechists.SingleAsync();
        Assert.Equal("Trần Văn An", teacher.FullName);
        Assert.Equal(f.Teacher, teacher.UserId);
    }
}
