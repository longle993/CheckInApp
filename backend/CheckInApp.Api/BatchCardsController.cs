using CheckInApp.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/cards/batch")]
public class BatchCardsController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPost]
    public async Task<object> Create(BatchCardsInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateBatchCardsCommand(Actor, input), ct);
        Response.Headers.CacheControl = "no-store";
        return result;
    }
}
