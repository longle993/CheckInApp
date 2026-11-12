using CheckInApp.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/users")]
public class AccountManagementController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPut("{id:guid}/profile")]
    public async Task<IActionResult> Edit(Guid id, EditUserInput input, CancellationToken ct)
    {
        await sender.Send(new EditUserCommand(Actor, id, input), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reconsider")]
    public async Task<IActionResult> Reconsider(Guid id, [FromBody] Dictionary<string, string> input, CancellationToken ct)
    {
        input.TryGetValue("reason", out var reason);
        await sender.Send(new ReconsiderUserCommand(Actor, id, reason), ct);
        return NoContent();
    }
}
