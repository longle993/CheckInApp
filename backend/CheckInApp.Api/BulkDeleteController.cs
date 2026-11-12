using CheckInApp.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/bulk-delete")]
public class BulkDeleteController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpPost("preview")] public Task<DeletePreview> Preview(DeleteSelection input, CancellationToken ct) => sender.Send(new BulkDeleteCommand(Actor, input, false), ct);
    [HttpPost] public Task<DeletePreview> Delete(DeleteSelection input, CancellationToken ct) => sender.Send(new BulkDeleteCommand(Actor, input, true), ct);
}
