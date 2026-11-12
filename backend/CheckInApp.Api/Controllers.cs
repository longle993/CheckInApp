using System.Text.Json;
using CheckInApp.Api.Security;
using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;

[ApiController, Route("api/v1/auth")]
public class AuthController(AuthService auth, AppDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("register"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterInput input, CancellationToken ct)
    {
        await auth.Register(input, ct);
        return Accepted(new
        {
            status = "PendingApproval",
            message = "Đăng ký thành công. Vui lòng chờ admin duyệt."
        });
    }
    [HttpPost("login"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken ct)
    {
        var pair = await auth.Login(input, ct);
        return Tokens(pair, input.Client);
    }
    [HttpPost("refresh"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshInput input, CancellationToken ct)
    {
        if (input.Client != "mobile" && Request.Headers["X-Requested-With"] != "CheckInApp")
            return BadRequest();
        var token = input.Client == "mobile" ? input.RefreshToken : Request.Cookies["ci_refresh"];
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized();
        return Tokens(await auth.Refresh(token, ct), input.Client);
    }
    [HttpGet("me"), Authorize] public async Task<object> Me(CancellationToken ct) => await auth.Me(UserId, ct);
    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var sid = Guid.Parse(User.FindFirst("sid")!.Value);
        var s = await db.AuthSessions.SingleAsync(x => x.Id == sid, ct);
        s.Revoked = true;
        s.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        Response.Cookies.Delete("ci_refresh", new CookieOptions { Path = "/api/v1/auth" });
        return NoContent();
    }
    Guid UserId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpPost("change-password"), Authorize, EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input, CancellationToken ct)
    {
        await auth.ChangePassword(UserId, input.CurrentPassword, input.NewPassword, input.ConfirmPassword, ct);
        Response.Cookies.Delete("ci_refresh", new CookieOptions { Path = "/api/v1/auth" });
        return NoContent();
    }
    IActionResult Tokens(TokenPair pair, string? client)
    {
        if (client == "mobile")
            return Ok(pair);
        Response.Cookies.Append("ci_refresh", pair.RefreshToken, new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            MaxAge = TimeSpan.FromDays(7)
        });
        return Ok(new
        {
            pair.AccessToken,
            pair.ExpiresAtUtc,
            pair.User
        });
    }
}
public record RefreshInput(string? RefreshToken, string? Client);
public record ChangePasswordInput(string CurrentPassword, string NewPassword, string ConfirmPassword);
public record ApproveInput(Guid RoleId, Guid? CatechistId);
public record CorrectionInput(bool Present, Guid Version, string Reason);
[ApiController, Authorize, Route("api/v1")]
public class DataController(ReadService read, AdminService admin, ISender sender) : ControllerBase
{
    Guid Actor => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet("catalog/{kind}")]
    public Task<object> List(
        string kind,
        string? q,
        Guid? classId,
        Guid? yearId,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default) => read.List(Actor, kind, q, classId, yearId, page, pageSize, ct);
    [HttpPost("catalog/{kind}")] public Task<object> Create(string kind, JsonElement data, CancellationToken ct) => sender.Send(new CatalogCommand(Actor, kind, null, data), ct);
    [HttpPut("catalog/{kind}/{id:guid}")] public Task<object> Update(string kind, Guid id, JsonElement data, CancellationToken ct) => sender.Send(new CatalogCommand(Actor, kind, id, data), ct);
    [HttpGet("attendance-sessions"), AspNetRole(Permission.AttendanceView)] public Task<object> Sessions(DateOnly from, DateOnly until, Guid? classId, Guid? yearId, CancellationToken ct) => read.Sessions(Actor, from, until, classId, ct, yearId);
    [HttpGet("attendance-sessions/{id:guid}/roster"), AspNetRole(Permission.AttendanceView)] public Task<object> Roster(Guid id, CancellationToken ct) => read.Roster(Actor, id, ct);
    [HttpPost("attendance-sessions/{id:guid}/check-ins"), AspNetRole(Permission.CheckIn)] public Task<CheckInResult> CheckIn(Guid id, CheckInInput input, CancellationToken ct) => sender.Send(new CheckInCommand(Actor, id, input), ct);
    [HttpPost("attendance-sessions/{id:guid}/students/check-ins"), AspNetRole(Permission.CheckIn)] public Task<CheckInResult> StudentScan(Guid id, CheckInInput input, CancellationToken ct) => sender.Send(new CheckInCommand(Actor, id, input with { PersonType = "Student" }), ct);
    [HttpPost("attendance-sessions/{id:guid}/catechists/check-ins"), AspNetRole(Permission.CheckIn | Permission.CatechistAttendance)] public Task<CheckInResult> TeacherScan(Guid id, CheckInInput input, CancellationToken ct) => sender.Send(new CheckInCommand(Actor, id, input with { PersonType = "Catechist" }), ct);
    [HttpPatch("attendance-records/{id:guid}"), AspNetRole(Permission.AttendanceCorrect)]
    public async Task<IActionResult> Correct(Guid id, CorrectionInput input, CancellationToken ct)
    {
        await admin.Correct(Actor, id, input.Present, input.Version, input.Reason, ct);
        return NoContent();
    }
    [HttpGet("reports/attendance"), AspNetRole(Permission.ReportView)] public Task<object> Report(DateOnly from, DateOnly until, Guid? classId, Guid? yearId, CancellationToken ct) => sender.Send(new ReportQuery(Actor, classId, from, until, yearId), ct);
    [HttpPost("students/{id:guid}/credential"), AspNetRole(Permission.CredentialManage)] public Task<object> Credential(Guid id, CancellationToken ct) => admin.Credential(Actor, id, ct);
    [HttpPost("catechists/{id:guid}/credential"), AspNetRole(Permission.CredentialManage)] public Task<object> CatechistCredential(Guid id, CancellationToken ct) => admin.CatechistCard(Actor, id, ct);
    [HttpPost("users/{id:guid}/approve"), AspNetRole(Permission.UserApprove | Permission.RoleManage)]
    public async Task<IActionResult> Approve(Guid id, ApproveInput input, CancellationToken ct)
    {
        await admin.Approve(Actor, id, input.RoleId, input.CatechistId, ct);
        return NoContent();
    }
    [HttpPost("users/{id:guid}/reject"), AspNetRole(Permission.UserApprove)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        await admin.Status(Actor, id, true, ct);
        return NoContent();
    }
    [HttpPost("users/{id:guid}/lock"), AspNetRole(Permission.UserApprove)]
    public async Task<IActionResult> Lock(Guid id, CancellationToken ct)
    {
        await admin.Status(Actor, id, false, ct);
        return NoContent();
    }
}
