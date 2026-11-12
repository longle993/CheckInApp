using CheckInApp.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/attendance-calendar")]
public class AttendanceCalendarController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public Task<object> Get(Guid yearId, string kind, Guid personId, int year, int month, Guid? classId, CancellationToken ct) =>
        sender.Send(new AttendanceCalendarQuery(Actor, yearId, kind, personId, year, month, classId), ct);
}
