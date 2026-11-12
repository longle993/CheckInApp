using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1/bulk-delete")]
public class BulkDeleteController(BulkDeleteService service) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpPost("preview")] public Task<DeletePreview> Preview(DeleteSelection input, CancellationToken ct) => service.Run(Actor, input, false, ct);
    [HttpPost] public Task<DeletePreview> Delete(DeleteSelection input, CancellationToken ct) => service.Run(Actor, input, true, ct);
}
