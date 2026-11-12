using CheckInApp.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/reports/timing")]
public class TimingReportController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public Task<object> Get(
        DateOnly from,
        DateOnly until,
        Guid yearId,
        Guid? classId,
        Guid? teacherId,
        string? personType,
        string? violation,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default) =>
        sender.Send(new TimingReportQuery(Actor, from, until, yearId, classId, teacherId, personType, violation, page, pageSize), ct);
}
