using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;
public class SessionScheduleTests
{
    [Fact]
    public async Task StartedSessionCannotBeEdited()
    {await using var f=new Fixture();await f.Init();var s=await f.Db.Sessions.SingleAsync();var error=await Assert.ThrowsAsync<AppException>(()=>new ScheduleService(f.Db,f.Clock).EditSession(f.User,s.Id,new(new(10,0),new(11,0),true,true,s.Version,"Test"),f.Access,default));Assert.Equal("SESSION_STARTED",error.Code);}
    [Theory]
    [InlineData(9,9)]
    [InlineData(9,10)]
    public async Task NewStartMustBeStrictlyAfterNow(int hour,int minute)
    {await using var f=new Fixture();await f.Init();var s=await f.Db.Sessions.SingleAsync();s.OpensAtUtc=new(2026,10,25,2,30,0);await f.Db.SaveChangesAsync();var error=await Assert.ThrowsAsync<AppException>(()=>new ScheduleService(f.Db,f.Clock).EditSession(f.User,s.Id,new(new(hour,minute),new(11,0),true,true,s.Version,"Test"),f.Access,default));Assert.Equal("SCHEDULE_TIME_INVALID",error.Code);}
    [Fact]
    public async Task FutureEditPersistsExceptionAndVersionAndRoster()
    {
        await using var f=new Fixture();await f.Init();var s=await f.Db.Sessions.SingleAsync();s.OpensAtUtc=new(2026,10,25,2,30,0);s.ClosesAtUtc=new(2026,10,25,4,0,0);
        f.Db.Schedules.Add(new(){ClassRoomId=f.ClassA,From=s.Date,Until=s.Date,DaysMask=127,OpensAt=new(9,30),ClosesAt=new(11,0)});await f.Db.SaveChangesAsync();var version=s.Version;
        var service=new ScheduleService(f.Db,f.Clock);var input=new SessionScheduleInput(new(9,11),new(10,0),false,true,version,"Đổi giờ buổi học");
        Assert.Equal(403,(await Assert.ThrowsAsync<AppException>(()=>service.EditSession(f.Teacher,s.Id,input,f.Access,default))).Status);
        await service.EditSession(f.User,s.Id,input,f.Access,default);Assert.NotEqual(version,s.Version);Assert.Empty(await f.Db.Rosters.ToListAsync());Assert.Single(await f.Db.Audit.Where(x=>x.Action=="EditSchedule").ToListAsync());
        Assert.Equal("VERSION_CONFLICT",(await Assert.ThrowsAsync<AppException>(()=>service.EditSession(f.User,s.Id,input,f.Access,default))).Code);
        await service.Synchronize(f.ClassA);Assert.Equal(new DateTime(2026,10,25,2,11,0),s.OpensAtUtc);Assert.False(s.StudentAttendanceEnabled);
    }
}
