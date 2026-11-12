using MediatR;

namespace CheckInApp.Application;

public record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, object User);
public record DeleteSelection(string Kind, Guid[] Ids, string? Fingerprint = null);
public record DeletePreview(string Fingerprint, Dictionary<string, int> Counts, string[] Names);
public record PresenceInput(DateTime CheckedInAtUtc, DateTime? CheckedOutAtUtc, Guid? Version, string Reason);
public record EditUserInput(
    string FullName,
    string Email,
    string Reason,
    string ExpectedFullName,
    string ExpectedEmail,
    Guid? RoleId = null,
    Guid? CatechistId = null,
    Guid? ProfileVersion = null,
    bool UpdateCatechistLink = false);
public record DashboardCell(string Key, int X, int Y, int W, int H);
public record DashboardLayout(
    string[] Widgets,
    string[] Shortcuts,
    Guid Version,
    Dictionary<string, DashboardCell[]>? Grids = null);
public record TimingReportRow(
    Guid SessionId,
    Guid PersonId,
    string PersonType,
    string FullName,
    string? SaintName,
    string Code,
    DateOnly Date,
    Guid ClassId,
    string ClassName,
    DateTime OpensAtUtc,
    DateTime ClosesAtUtc,
    DateTime InAtUtc,
    DateTime? OutAtUtc,
    int LateMinutes,
    int EarlyMinutes,
    string Teachers,
    bool Adjusted);
public record BatchCardsInput(Guid RequestId, string Kind, Guid[] Ids, bool ConfirmReissue, Guid? YearId = null);
public record AvatarInput(string? PngBase64, bool Remove);
public record StoredFile(string Path, string ContentType);
public record WebSettingsInput(string Name, string? LogoBase64, bool RemoveLogo, Guid Version);
public record ExcelUpload(string Base64, bool OverwriteExisting = false, Guid? ClassId = null, Guid? YearId = null);
public record ExportedFile(byte[] Data, string ContentType, string FileName);
public record SessionScheduleInput(
    TimeOnly OpensAt,
    TimeOnly ClosesAt,
    bool StudentAttendanceEnabled,
    bool CatechistAttendanceEnabled,
    Guid Version,
    string Reason,
    DateOnly? Date = null);

public interface IAuthService
{
    Task Register(RegisterInput input, CancellationToken ct);
    Task<TokenPair> Login(LoginInput input, CancellationToken ct);
    Task<TokenPair> Refresh(string token, CancellationToken ct);
    Task<object> Me(Guid actorId, CancellationToken ct);
    Task Logout(Guid sessionId, CancellationToken ct);
    Task ChangePassword(Guid actorId, string currentPassword, string newPassword, string confirmation, CancellationToken ct);
}

public interface IReadService
{
    Task<object> List(Guid actorId, string kind, string? search, Guid? classId, Guid? yearId, int page, int pageSize, CancellationToken ct);
    Task<object> Sessions(Guid actorId, DateOnly from, DateOnly until, Guid? classId, CancellationToken ct, Guid? yearId = null);
    Task<object> Roster(Guid actorId, Guid sessionId, CancellationToken ct);
}

public interface IAdminService
{
    Task Correct(Guid actorId, Guid recordId, bool present, Guid version, string reason, CancellationToken ct);
    Task<object> Credential(Guid actorId, Guid studentId, CancellationToken ct);
    Task<object> CatechistCard(Guid actorId, Guid catechistId, CancellationToken ct);
    Task Approve(Guid actorId, Guid userId, Guid roleId, Guid? catechistId, CancellationToken ct);
    Task Status(Guid actorId, Guid userId, bool reject, CancellationToken ct);
}

public interface IBulkDeleteService
{
    Task<DeletePreview> Run(Guid actorId, DeleteSelection input, bool execute, CancellationToken ct);
}

public interface IScheduleService
{
    Task EditSession(Guid actorId, Guid sessionId, SessionScheduleInput input, CancellationToken ct);
}

public interface IPresenceService
{
    Task<object> List(Guid actorId, Guid sessionId, CancellationToken ct);
    Task Save(Guid actorId, Guid sessionId, Guid personId, bool teacher, PresenceInput input, CancellationToken ct);
}

public interface IAccountService
{
    Task Edit(Guid actorId, Guid userId, EditUserInput input, CancellationToken ct);
    Task Reconsider(Guid actorId, Guid userId, string? reason, CancellationToken ct);
}

public interface IDashboardService
{
    Task<DashboardLayout> Get(Guid actorId, CancellationToken ct);
    Task<DashboardLayout> Save(Guid actorId, DashboardLayout input, CancellationToken ct);
}

public interface IReportingService
{
    Task<object> AttendanceCalendar(Guid actorId, Guid yearId, string kind, Guid personId, int year, int month, Guid? classId, CancellationToken ct);
    Task<object> Timing(Guid actorId, DateOnly from, DateOnly until, Guid yearId, Guid? classId, Guid? teacherId, string? personType, string? violation, int page, int pageSize, CancellationToken ct);
}

public interface IBatchCardsService
{
    Task<object> Create(Guid actorId, BatchCardsInput input, CancellationToken ct);
}

public interface IAvatarService
{
    Task<StoredFile?> Get(Guid actorId, string kind, Guid personId, CancellationToken ct);
    Task Save(Guid actorId, string kind, Guid personId, AvatarInput input, CancellationToken ct);
}

public interface IWebSettingsService
{
    Task<object> Public(CancellationToken ct);
    Task<object> Admin(Guid actorId, CancellationToken ct);
    Task<StoredFile?> Logo(CancellationToken ct);
    Task<object> Save(Guid actorId, WebSettingsInput input, CancellationToken ct);
}

public interface IExcelService
{
    Task<ExportedFile> Export(Guid actorId, string kind, Guid? classId, Guid? yearId, bool template, CancellationToken ct);
    Task<object> Import(Guid actorId, string kind, ExcelUpload input, CancellationToken ct);
}

public record RegisterCommand(RegisterInput Input) : IRequest;
public record LoginCommand(LoginInput Input) : IRequest<TokenPair>;
public record RefreshCommand(string Token) : IRequest<TokenPair>;
public record CurrentUserQuery(Guid ActorId) : IRequest<object>;
public record LogoutCommand(Guid SessionId) : IRequest;
public record ChangePasswordCommand(Guid ActorId, string CurrentPassword, string NewPassword, string Confirmation) : IRequest;
public record CatalogListQuery(Guid ActorId, string Kind, string? Search, Guid? ClassId, Guid? YearId, int Page, int PageSize) : IRequest<object>;
public record AttendanceSessionsQuery(Guid ActorId, DateOnly From, DateOnly Until, Guid? ClassId, Guid? YearId) : IRequest<object>;
public record RosterQuery(Guid ActorId, Guid SessionId) : IRequest<object>;
public record CorrectAttendanceCommand(Guid ActorId, Guid RecordId, bool Present, Guid Version, string Reason) : IRequest;
public record IssueCredentialCommand(Guid ActorId, Guid PersonId, bool Catechist) : IRequest<object>;
public record ApproveUserCommand(Guid ActorId, Guid UserId, Guid RoleId, Guid? CatechistId) : IRequest;
public record SetUserStatusCommand(Guid ActorId, Guid UserId, bool Reject) : IRequest;
public record BulkDeleteCommand(Guid ActorId, DeleteSelection Input, bool Execute) : IRequest<DeletePreview>;
public record EditSessionCommand(Guid ActorId, Guid SessionId, SessionScheduleInput Input) : IRequest;
public record PresenceQuery(Guid ActorId, Guid SessionId) : IRequest<object>;
public record SavePresenceCommand(Guid ActorId, Guid SessionId, Guid PersonId, bool Teacher, PresenceInput Input) : IRequest;
public record EditUserCommand(Guid ActorId, Guid UserId, EditUserInput Input) : IRequest;
public record ReconsiderUserCommand(Guid ActorId, Guid UserId, string? Reason) : IRequest;
public record DashboardQuery(Guid ActorId) : IRequest<DashboardLayout>;
public record SaveDashboardCommand(Guid ActorId, DashboardLayout Input) : IRequest<DashboardLayout>;
public record AttendanceCalendarQuery(Guid ActorId, Guid YearId, string Kind, Guid PersonId, int Year, int Month, Guid? ClassId) : IRequest<object>;
public record TimingReportQuery(Guid ActorId, DateOnly From, DateOnly Until, Guid YearId, Guid? ClassId, Guid? TeacherId, string? PersonType, string? Violation, int Page, int PageSize) : IRequest<object>;
public record CreateBatchCardsCommand(Guid ActorId, BatchCardsInput Input) : IRequest<object>;
public record AvatarQuery(Guid ActorId, string Kind, Guid PersonId) : IRequest<StoredFile?>;
public record SaveAvatarCommand(Guid ActorId, string Kind, Guid PersonId, AvatarInput Input) : IRequest;
public record PublicWebSettingsQuery : IRequest<object>;
public record AdminWebSettingsQuery(Guid ActorId) : IRequest<object>;
public record WebLogoQuery : IRequest<StoredFile?>;
public record SaveWebSettingsCommand(Guid ActorId, WebSettingsInput Input) : IRequest<object>;
public record ExportExcelQuery(Guid ActorId, string Kind, Guid? ClassId, Guid? YearId, bool Template) : IRequest<ExportedFile>;
public record ImportExcelCommand(Guid ActorId, string Kind, ExcelUpload Input) : IRequest<object>;

public sealed class RegisterHandler(IAuthService service) : IRequestHandler<RegisterCommand>
{
    public Task Handle(RegisterCommand request, CancellationToken ct) => service.Register(request.Input, ct);
}

public sealed class LoginHandler(IAuthService service) : IRequestHandler<LoginCommand, TokenPair>
{
    public Task<TokenPair> Handle(LoginCommand request, CancellationToken ct) => service.Login(request.Input, ct);
}

public sealed class RefreshHandler(IAuthService service) : IRequestHandler<RefreshCommand, TokenPair>
{
    public Task<TokenPair> Handle(RefreshCommand request, CancellationToken ct) => service.Refresh(request.Token, ct);
}

public sealed class CurrentUserHandler(IAuthService service) : IRequestHandler<CurrentUserQuery, object>
{
    public Task<object> Handle(CurrentUserQuery request, CancellationToken ct) => service.Me(request.ActorId, ct);
}

public sealed class LogoutHandler(IAuthService service) : IRequestHandler<LogoutCommand>
{
    public Task Handle(LogoutCommand request, CancellationToken ct) => service.Logout(request.SessionId, ct);
}

public sealed class ChangePasswordHandler(IAuthService service) : IRequestHandler<ChangePasswordCommand>
{
    public Task Handle(ChangePasswordCommand request, CancellationToken ct) =>
        service.ChangePassword(request.ActorId, request.CurrentPassword, request.NewPassword, request.Confirmation, ct);
}

public sealed class CatalogListHandler(IReadService service) : IRequestHandler<CatalogListQuery, object>
{
    public Task<object> Handle(CatalogListQuery request, CancellationToken ct) =>
        service.List(request.ActorId, request.Kind, request.Search, request.ClassId, request.YearId, request.Page, request.PageSize, ct);
}

public sealed class AttendanceSessionsHandler(IReadService service) : IRequestHandler<AttendanceSessionsQuery, object>
{
    public Task<object> Handle(AttendanceSessionsQuery request, CancellationToken ct) =>
        service.Sessions(request.ActorId, request.From, request.Until, request.ClassId, ct, request.YearId);
}

public sealed class RosterHandler(IReadService service) : IRequestHandler<RosterQuery, object>
{
    public Task<object> Handle(RosterQuery request, CancellationToken ct) => service.Roster(request.ActorId, request.SessionId, ct);
}

public sealed class CorrectAttendanceHandler(IAdminService service) : IRequestHandler<CorrectAttendanceCommand>
{
    public Task Handle(CorrectAttendanceCommand request, CancellationToken ct) =>
        service.Correct(request.ActorId, request.RecordId, request.Present, request.Version, request.Reason, ct);
}

public sealed class IssueCredentialHandler(IAdminService service) : IRequestHandler<IssueCredentialCommand, object>
{
    public Task<object> Handle(IssueCredentialCommand request, CancellationToken ct) => request.Catechist
        ? service.CatechistCard(request.ActorId, request.PersonId, ct)
        : service.Credential(request.ActorId, request.PersonId, ct);
}

public sealed class ApproveUserHandler(IAdminService service) : IRequestHandler<ApproveUserCommand>
{
    public Task Handle(ApproveUserCommand request, CancellationToken ct) =>
        service.Approve(request.ActorId, request.UserId, request.RoleId, request.CatechistId, ct);
}

public sealed class SetUserStatusHandler(IAdminService service) : IRequestHandler<SetUserStatusCommand>
{
    public Task Handle(SetUserStatusCommand request, CancellationToken ct) =>
        service.Status(request.ActorId, request.UserId, request.Reject, ct);
}

public sealed class BulkDeleteHandler(IBulkDeleteService service) : IRequestHandler<BulkDeleteCommand, DeletePreview>
{
    public Task<DeletePreview> Handle(BulkDeleteCommand request, CancellationToken ct) =>
        service.Run(request.ActorId, request.Input, request.Execute, ct);
}

public sealed class EditSessionHandler(IScheduleService service) : IRequestHandler<EditSessionCommand>
{
    public Task Handle(EditSessionCommand request, CancellationToken ct) =>
        service.EditSession(request.ActorId, request.SessionId, request.Input, ct);
}

public sealed class PresenceHandler(IPresenceService service) : IRequestHandler<PresenceQuery, object>
{
    public Task<object> Handle(PresenceQuery request, CancellationToken ct) => service.List(request.ActorId, request.SessionId, ct);
}

public sealed class SavePresenceHandler(IPresenceService service) : IRequestHandler<SavePresenceCommand>
{
    public Task Handle(SavePresenceCommand request, CancellationToken ct) =>
        service.Save(request.ActorId, request.SessionId, request.PersonId, request.Teacher, request.Input, ct);
}

public sealed class EditUserHandler(IAccountService service) : IRequestHandler<EditUserCommand>
{
    public Task Handle(EditUserCommand request, CancellationToken ct) => service.Edit(request.ActorId, request.UserId, request.Input, ct);
}

public sealed class ReconsiderUserHandler(IAccountService service) : IRequestHandler<ReconsiderUserCommand>
{
    public Task Handle(ReconsiderUserCommand request, CancellationToken ct) =>
        service.Reconsider(request.ActorId, request.UserId, request.Reason, ct);
}

public sealed class DashboardHandler(IDashboardService service) : IRequestHandler<DashboardQuery, DashboardLayout>
{
    public Task<DashboardLayout> Handle(DashboardQuery request, CancellationToken ct) => service.Get(request.ActorId, ct);
}

public sealed class SaveDashboardHandler(IDashboardService service) : IRequestHandler<SaveDashboardCommand, DashboardLayout>
{
    public Task<DashboardLayout> Handle(SaveDashboardCommand request, CancellationToken ct) => service.Save(request.ActorId, request.Input, ct);
}

public sealed class AttendanceCalendarHandler(IReportingService service) : IRequestHandler<AttendanceCalendarQuery, object>
{
    public Task<object> Handle(AttendanceCalendarQuery request, CancellationToken ct) =>
        service.AttendanceCalendar(request.ActorId, request.YearId, request.Kind, request.PersonId, request.Year, request.Month, request.ClassId, ct);
}

public sealed class TimingReportHandler(IReportingService service) : IRequestHandler<TimingReportQuery, object>
{
    public Task<object> Handle(TimingReportQuery request, CancellationToken ct) =>
        service.Timing(request.ActorId, request.From, request.Until, request.YearId, request.ClassId, request.TeacherId, request.PersonType, request.Violation, request.Page, request.PageSize, ct);
}

public sealed class CreateBatchCardsHandler(IBatchCardsService service) : IRequestHandler<CreateBatchCardsCommand, object>
{
    public Task<object> Handle(CreateBatchCardsCommand request, CancellationToken ct) => service.Create(request.ActorId, request.Input, ct);
}

public sealed class AvatarHandler(IAvatarService service) : IRequestHandler<AvatarQuery, StoredFile?>
{
    public Task<StoredFile?> Handle(AvatarQuery request, CancellationToken ct) =>
        service.Get(request.ActorId, request.Kind, request.PersonId, ct);
}

public sealed class SaveAvatarHandler(IAvatarService service) : IRequestHandler<SaveAvatarCommand>
{
    public Task Handle(SaveAvatarCommand request, CancellationToken ct) =>
        service.Save(request.ActorId, request.Kind, request.PersonId, request.Input, ct);
}

public sealed class PublicWebSettingsHandler(IWebSettingsService service) : IRequestHandler<PublicWebSettingsQuery, object>
{
    public Task<object> Handle(PublicWebSettingsQuery request, CancellationToken ct) => service.Public(ct);
}

public sealed class AdminWebSettingsHandler(IWebSettingsService service) : IRequestHandler<AdminWebSettingsQuery, object>
{
    public Task<object> Handle(AdminWebSettingsQuery request, CancellationToken ct) => service.Admin(request.ActorId, ct);
}

public sealed class WebLogoHandler(IWebSettingsService service) : IRequestHandler<WebLogoQuery, StoredFile?>
{
    public Task<StoredFile?> Handle(WebLogoQuery request, CancellationToken ct) => service.Logo(ct);
}

public sealed class SaveWebSettingsHandler(IWebSettingsService service) : IRequestHandler<SaveWebSettingsCommand, object>
{
    public Task<object> Handle(SaveWebSettingsCommand request, CancellationToken ct) => service.Save(request.ActorId, request.Input, ct);
}

public sealed class ExportExcelHandler(IExcelService service) : IRequestHandler<ExportExcelQuery, ExportedFile>
{
    public Task<ExportedFile> Handle(ExportExcelQuery request, CancellationToken ct) =>
        service.Export(request.ActorId, request.Kind, request.ClassId, request.YearId, request.Template, ct);
}

public sealed class ImportExcelHandler(IExcelService service) : IRequestHandler<ImportExcelCommand, object>
{
    public Task<object> Handle(ImportExcelCommand request, CancellationToken ct) =>
        service.Import(request.ActorId, request.Kind, request.Input, ct);
}
