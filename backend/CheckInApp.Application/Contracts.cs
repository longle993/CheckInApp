using System.Text.Json;
using CheckInApp.Domain;
using MediatR;
namespace CheckInApp.Application;

public sealed class AppException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status; public string Code { get; } = code;
}
public record Actor(Guid Id);
public record LoginInput(string Email, string Password, string? Client);
public record RegisterInput(string Email, string Password, string FullName);
public record CheckInInput(string Credential, string ClientRequestId, string Method = "QR");
public record CheckInResult(string ResultCode, Guid AttendanceRecordId, Guid StudentId, string StudentName, string StudentCode, bool Present, DateTime RecordedAtUtc, Guid Version, string PersonType = "Student");
public record CheckInCommand(Guid ActorId, Guid SessionId, CheckInInput Input) : IRequest<CheckInResult>;
public record ReportQuery(Guid ActorId, Guid? ClassId, DateOnly From, DateOnly Until, Guid? YearId = null) : IRequest<object>;
public record CatalogCommand(Guid ActorId, string Kind, Guid? Id, JsonElement Data) : IRequest<object>;
public interface IAttendanceService
{
    Task<CheckInResult> CheckIn(CheckInCommand command, CancellationToken ct); Task<object> Report(ReportQuery query, CancellationToken ct);
}
public interface ICatalogService
{
    Task<object> Save(CatalogCommand command, CancellationToken ct);
}
public sealed class CheckInHandler(IAttendanceService service) : IRequestHandler<CheckInCommand, CheckInResult>
{
    public Task<CheckInResult> Handle(CheckInCommand r, CancellationToken ct) => service.CheckIn(r, ct);
}
public sealed class ReportHandler(IAttendanceService service) : IRequestHandler<ReportQuery, object>
{
    public Task<object> Handle(ReportQuery r, CancellationToken ct) => service.Report(r, ct);
}
public sealed class CatalogHandler(ICatalogService service) : IRequestHandler<CatalogCommand, object>
{
    public Task<object> Handle(CatalogCommand r, CancellationToken ct) => service.Save(r, ct);
}
