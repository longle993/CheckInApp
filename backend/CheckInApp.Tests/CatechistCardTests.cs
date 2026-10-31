using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;
public class CatechistCardTests
{
    static async Task<string> Issue(Fixture f, Guid teacher) => JsonSerializer.SerializeToElement(await new AdminService(f.Db,f.Access,f.Clock).CatechistCard(f.User,teacher,default)).GetProperty("credential").GetString()!;
    [Fact]
    public async Task QrBarcodeAndRetriesShareOneTeacherRecord()
    {
        await using var f=new Fixture();await f.Init();var teacher=await f.Db.Catechists.SingleAsync();var token=await Issue(f,teacher.Id);
        var first=await f.Service.CheckIn(new(f.User,f.Session,new(token,"teacher-qr-0001","QR")),default);
        var replay=await f.Service.CheckIn(new(f.User,f.Session,new(token,"teacher-qr-0001","QR")),default);
        var barcode=await f.Service.CheckIn(new(f.User,f.Session,new(token,"teacher-bar-0001","Barcode")),default);
        Assert.Equal("Catechist",first.PersonType);Assert.Equal(first,replay);Assert.Equal("AlreadyCheckedIn",barcode.ResultCode);
        Assert.Single(await f.Db.CatechistAttendance.ToListAsync());Assert.Empty(await f.Db.Attendance.ToListAsync());
    }
    [Fact]
    public async Task ReissueRevokesPreviousTeacherCard()
    {
        await using var f=new Fixture();await f.Init();var teacher=await f.Db.Catechists.SingleAsync();var old=await Issue(f,teacher.Id);var fresh=await Issue(f,teacher.Id);
        Assert.Equal(400,(await Assert.ThrowsAsync<AppException>(()=>f.Service.CheckIn(new(f.User,f.Session,new(old,"revoked-card-001")),default))).Status);
        Assert.Equal("Created",(await f.Service.CheckIn(new(f.User,f.Session,new(fresh,"fresh-card-0001")),default)).ResultCode);
        Assert.Single(await f.Db.CatechistCredentials.Where(x=>x.RevokedAtUtc==null).ToListAsync());
    }
    [Fact]
    public async Task TeacherCardChecksPermissionTargetAndAssignment()
    {
        await using var f=new Fixture();await f.Init();var teacher=await f.Db.Catechists.SingleAsync();var token=await Issue(f,teacher.Id);
        Assert.Equal(403,(await Assert.ThrowsAsync<AppException>(()=>f.Service.CheckIn(new(f.Teacher,f.Session,new(token,"denied-scan-001")),default))).Status);
        var session=await f.Db.Sessions.SingleAsync();session.CatechistAttendanceEnabled=false;await f.Db.SaveChangesAsync();
        Assert.Equal(409,(await Assert.ThrowsAsync<AppException>(()=>f.Service.CheckIn(new(f.User,f.Session,new(token,"disabled-glv-01")),default))).Status);
        session.CatechistAttendanceEnabled=true;session.StudentAttendanceEnabled=false;await f.Db.SaveChangesAsync();
        Assert.Equal("Created",(await f.Service.CheckIn(new(f.User,f.Session,new(token,"glv-only-0001")),default)).ResultCode);
        f.Db.Assignments.RemoveRange(await f.Db.Assignments.ToListAsync());await f.Db.SaveChangesAsync();
        Assert.Equal("WRONG_CLASS",(await Assert.ThrowsAsync<AppException>(()=>f.Service.CheckIn(new(f.User,f.Session,new(token,"wrong-class-001")),default))).Code);
    }
}
