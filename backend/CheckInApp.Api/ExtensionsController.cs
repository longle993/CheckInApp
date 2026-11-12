using CheckInApp.Api.Security;
using CheckInApp.Application;
using CheckInApp.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInApp.Api;

[ApiController, Authorize, Route("api/v1")]
public class ExtensionsController(ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpPut("attendance-sessions/{id:guid}/schedule"), AspNetRole(Permission.ScheduleManage)]
    public async Task<IActionResult> EditSchedule(Guid id, SessionScheduleInput input, CancellationToken ct)
    {
        await sender.Send(new EditSessionCommand(Actor, id, input), ct);
        return NoContent();
    }

    [HttpGet("attendance-sessions/{id:guid}/catechists"), AspNetRole(Permission.AttendanceView)]
    public Task<object> Teachers(Guid id, CancellationToken ct) => sender.Send(new PresenceQuery(Actor, id), ct);

    [HttpPut("attendance-sessions/{id:guid}/times/{personId:guid}")]
    public async Task<IActionResult> Times(Guid id, Guid personId, PresenceInput input, CancellationToken ct, bool teacher = false)
    {
        await sender.Send(new SavePresenceCommand(Actor, id, personId, teacher, input), ct);
        return NoContent();
    }

    [HttpGet("excel/{kind}")]
    public async Task<IActionResult> Export(
        string kind,
        Guid? classId,
        Guid? yearId,
        bool template = false,
        CancellationToken ct = default)
    {
        var file = await sender.Send(new ExportExcelQuery(Actor, kind, classId, yearId, template), ct);
        return File(file.Data, file.ContentType, file.FileName);
    }

    [HttpPost("excel/{kind}"), RequestSizeLimit(7_000_000)]
    public Task<object> Import(string kind, ExcelUpload input, CancellationToken ct) =>
        sender.Send(new ImportExcelCommand(Actor, kind, input), ct);
}
