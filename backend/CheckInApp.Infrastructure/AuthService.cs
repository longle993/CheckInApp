using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
namespace CheckInApp.Infrastructure;

public record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, object User);
public class AuthService(AppDbContext db, IConfiguration config, AccessService access, TimeProvider clock)
{
    public async Task ChangePassword(Guid actor, string currentPassword, string newPassword, string confirmation, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(currentPassword) || currentPassword.Length>128) throw new AppException(400,"CURRENT_PASSWORD","Nhập mật khẩu hiện tại hợp lệ.");
        if(newPassword!=confirmation) throw new AppException(400,"PASSWORD_MISMATCH","Xác nhận mật khẩu mới không khớp.");
        ValidatePassword(newPassword);
        if(currentPassword==newPassword) throw new AppException(400,"PASSWORD_UNCHANGED","Mật khẩu mới phải khác mật khẩu hiện tại.");
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var query=db.Database.IsSqlServer()?db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {actor}"):db.Users.Where(x=>x.Id==actor);
        var user=await query.SingleOrDefaultAsync(ct);
        if(user==null || user.Status!=AccountStatus.Active) throw new AppException(403,"ACCOUNT_INACTIVE","Tài khoản chưa hoạt động.");
        var hasher=new PasswordHasher<AppUser>();
        if(hasher.VerifyHashedPassword(user,user.PasswordHash,currentPassword)==PasswordVerificationResult.Failed) throw new AppException(400,"CURRENT_PASSWORD","Mật khẩu hiện tại không đúng.");
        user.PasswordHash=hasher.HashPassword(user,newPassword);
        foreach(var session in await db.AuthSessions.Where(x=>x.UserId==actor&&!x.Revoked).ToListAsync(ct)){session.Revoked=true;session.Version=Guid.NewGuid();}
        db.Audit.Add(new() {ActorId=actor,Action="ChangePassword",EntityType="User",EntityId=actor});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string RandomToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static void ValidatePassword(string password)
    {
        if (password.Length < 12 || password.Length > 128 || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new AppException(400, "WEAK_PASSWORD", "Mật khẩu cần 12–128 ký tự, có chữ và số.");
    }
    public async Task Register(RegisterInput input, CancellationToken ct)
    {
        ValidatePassword(input.Password);
        var email = input.Email.Trim().ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
            throw new AppException(400, "EMAIL_INVALID", "Email không hợp lệ.");
        if (input.FullName.Trim().Length is < 2 or > 150)
            throw new AppException(400, "NAME_INVALID", "Họ tên cần 2–150 ký tự.");
        if (await db.Users.AnyAsync(x => x.Email == email, ct))
            throw new AppException(409, "EMAIL_EXISTS", "Email đã được đăng ký.");
        var u = new AppUser { Email = email, FullName = input.FullName.Trim(), Status = AccountStatus.PendingApproval };
        u.PasswordHash = new PasswordHasher<AppUser>().HashPassword(u, input.Password);
        db.Users.Add(u);
        await db.SaveChangesAsync(ct);
    }
    public async Task<TokenPair> Login(LoginInput input, CancellationToken ct)
    {
        var email = input.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user == null || new PasswordHasher<AppUser>().VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
            throw new AppException(401, "LOGIN_FAILED", "Email hoặc mật khẩu không đúng.");
        if (user.Status != AccountStatus.Active)
            throw new AppException(403, "ACCOUNT_" + user.Status.ToString().ToUpperInvariant(), user.Status == AccountStatus.PendingApproval ? "Tài khoản đang chờ admin duyệt." : "Tài khoản chưa được phép sử dụng.");
        var session = new AuthSession { UserId = user.Id, ExpiresAtUtc = clock.GetUtcNow().UtcDateTime.AddDays(7) };
        db.AuthSessions.Add(session);
        var refresh = RandomToken();
        db.RefreshTokens.Add(new()
        {
            AuthSessionId = session.Id,
            Hash = Hash(refresh)
        });
        await db.SaveChangesAsync(ct);
        return await Issue(user, session, refresh, ct);
    }
    public async Task<TokenPair> Refresh(string token, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var hash = Hash(token);
        var entry = await db.RefreshTokens.SingleOrDefaultAsync(x => x.Hash == hash, ct);
        if (entry == null)
            throw new AppException(401, "REFRESH_INVALID", "Phiên đăng nhập không hợp lệ.");
        var session = await db.AuthSessions.SingleAsync(x => x.Id == entry.AuthSessionId, ct);
        var user = await db.Users.SingleAsync(x => x.Id == session.UserId, ct);
        if (entry.Used)
        {
            session.Revoked = true;
            session.Version = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new AppException(401, "TOKEN_REUSED", "Phiên đã bị thu hồi. Vui lòng đăng nhập lại.");
        }
        if (session.Revoked || session.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime || user.Status != AccountStatus.Active)
            throw new AppException(401, "SESSION_EXPIRED", "Phiên đã hết hạn hoặc bị thu hồi.");
        entry.Used = true;
        entry.Version = Guid.NewGuid();
        session.Version = Guid.NewGuid();
        var fresh = RandomToken();
        db.RefreshTokens.Add(new()
        {
            Hash = Hash(fresh),
            AuthSessionId = session.Id
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await Issue(user, session, fresh, ct);
    }
    public async Task<object> Me(Guid id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var rights = await access.Rights(id, ct);
        return new
        {
            id = u.Id,
            u.Email,
            u.FullName,
            status = u.Status.ToString(),
            permissions = rights.Mask.ToString(),
            globalScope = rights.Global
        };
    }
    private async Task<TokenPair> Issue(AppUser user, AuthSession session, string refresh, CancellationToken ct)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(15);
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key missing");
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim("sid", session.Id.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) }, notBefore: clock.GetUtcNow().UtcDateTime, expires: expires, signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, expires, await Me(user.Id, ct));
    }
}
