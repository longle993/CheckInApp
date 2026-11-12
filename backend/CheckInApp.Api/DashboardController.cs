using CheckInApp.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/dashboard-layout")]
public class DashboardController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public Task<DashboardLayout> Get(CancellationToken ct) => sender.Send(new DashboardQuery(Actor), ct);

    [HttpPut]
    public Task<DashboardLayout> Put(DashboardLayout input, CancellationToken ct) =>
        sender.Send(new SaveDashboardCommand(Actor, input), ct);
}
