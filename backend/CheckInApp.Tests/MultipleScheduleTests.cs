using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;
public class MultipleScheduleTests
{
    [Fact]
    public async Task ReportsSeparateSessionsFromCompletedDays()
    {
        await using var f=new Fixture();await f.Init();var first=await f.Db.Sessions.SingleAsync();first.ClosesAtUtc=new(2026,10,25,2,9,0);var second=new AttendanceSession {ClassRoomId=f.ClassA,Date=first.Date,OpensAtUtc=new(2026,10,25,1,0,0),ClosesAtUtc=new(2026,10,25,1,30,0)};f.Db.Sessions.Add(second);f.Db.Rosters.Add(new(){SessionId=second.Id,StudentId=f.Student,EnrollmentId=f.Enrollment});f.Db.Attendance.Add(new(){SessionId=first.Id,StudentId=f.Student,ActorId=f.User,RecordedAtUtc=new(2026,10,25,2,1,0)});await f.Db.SaveChangesAsync();
        var report=System.Text.Json.JsonSerializer.SerializeToElement(await f.Service.Report(new(f.User,f.ClassA,first.Date,first.Date),default));var row=report.GetProperty("students")[0];Assert.Equal(2,row.GetProperty("expected").GetInt32());Assert.Equal(1,row.GetProperty("present").GetInt32());Assert.Equal(1,row.GetProperty("expectedDays").GetInt32());Assert.Equal(0,row.GetProperty("presentDays").GetInt32());Assert.Equal(1,row.GetProperty("absentDays").GetInt32());Assert.Equal(1,row.GetProperty("partialDays").GetInt32());
    }
    [Fact]
    public async Task AdjacentSlotsAndMovedDayKeepOccurrenceIdentity()
    {
        await using var f=new Fixture();await f.Init();var a=new AttendanceSchedule{ClassRoomId=f.ClassA,From=new(2026,10,26),Until=new(2026,10,27),DaysMask=127,OpensAt=new(9,0),ClosesAt=new(10,0)};var b=new AttendanceSchedule{ClassRoomId=f.ClassA,From=a.From,Until=a.Until,DaysMask=127,OpensAt=new(10,0),ClosesAt=new(11,0)};f.Db.Schedules.AddRange(a,b);await f.Db.SaveChangesAsync();var service=new ScheduleService(f.Db,f.Clock);await service.Synchronize(f.ClassA);Assert.Equal(4,await f.Db.Sessions.CountAsync(x=>x.Date>=a.From));
        var first=await f.Db.Sessions.SingleAsync(x=>x.ScheduleId==a.Id&&x.Date==a.From);var id=first.Id;
        await service.EditSession(f.User,id,new(new(11,0),new(12,0),true,true,first.Version,"Dời ngày",new(2026,10,27)),f.Access,default);
        await service.Synchronize(f.ClassA);Assert.Equal(1,await f.Db.Sessions.CountAsync(x=>x.Date==a.From));Assert.Equal(3,await f.Db.Sessions.CountAsync(x=>x.Date==a.Until));Assert.Equal(a.Until,(await f.Db.Sessions.SingleAsync(x=>x.Id==id)).Date);
        Assert.Equal("SCHEDULE_OVERLAP",(await Assert.ThrowsAsync<AppException>(()=>service.EditSession(f.User,id,new(new(9,30),new(10,30),true,true,first.Version,"Trùng",a.Until),f.Access,default))).Code);
    }
    [Fact]
    public async Task OverlappingRecurringSlotsAreRejected()
    {
        await using var f=new Fixture();await f.Init();f.Db.Schedules.AddRange(new AttendanceSchedule{ClassRoomId=f.ClassA,From=new(2026,10,26),Until=new(2026,10,26),DaysMask=127,OpensAt=new(9,0),ClosesAt=new(10,0)},new AttendanceSchedule{ClassRoomId=f.ClassA,From=new(2026,10,26),Until=new(2026,10,26),DaysMask=127,OpensAt=new(9,30),ClosesAt=new(10,30)});await f.Db.SaveChangesAsync();Assert.Equal("SCHEDULE_OVERLAP",(await Assert.ThrowsAsync<AppException>(()=>new ScheduleService(f.Db,f.Clock).Synchronize(f.ClassA))).Code);
    }
}
