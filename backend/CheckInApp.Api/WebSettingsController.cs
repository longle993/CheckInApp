using CheckInApp.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInApp.Api;

[ApiController, Route("api/v1/web-settings")]
public class WebSettingsController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet, AllowAnonymous]
    public Task<object> Public(CancellationToken ct) => sender.Send(new PublicWebSettingsQuery(), ct);

    [HttpGet("admin"), Authorize]
    public Task<object> Admin(CancellationToken ct) => sender.Send(new AdminWebSettingsQuery(Actor), ct);

    [HttpGet("logo"), AllowAnonymous]
    public async Task<IActionResult> Logo(CancellationToken ct)
    {
        var file = await sender.Send(new WebLogoQuery(), ct);
        return file == null ? NotFound() : PhysicalFile(file.Path, file.ContentType);
    }

    [HttpPut, Authorize, RequestSizeLimit(3_000_000)]
    public Task<object> Save(WebSettingsInput input, CancellationToken ct) =>
        sender.Send(new SaveWebSettingsCommand(Actor, input), ct);
}
