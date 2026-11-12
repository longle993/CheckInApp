using CheckInApp.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/people/{kind}/{id:guid}/avatar")]
public class AvatarController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> Get(string kind, Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        var file = await sender.Send(new AvatarQuery(Actor, kind, id), ct);
        return file == null ? NotFound() : PhysicalFile(file.Path, file.ContentType);
    }

    [HttpPut, RequestSizeLimit(3_000_000)]
    public async Task<IActionResult> Put(string kind, Guid id, AvatarInput input, CancellationToken ct)
    {
        await sender.Send(new SaveAvatarCommand(Actor, kind, id, input), ct);
        return NoContent();
    }
}
