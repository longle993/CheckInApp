using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace CheckInApp.Tests;

public class PasswordChangeTests
{
    [Fact]
    public async Task ChangeRequiresCurrentPasswordAndRevokesAllSessions()
    {
        await using var f = new Fixture();
        await f.Init();
        var user = await f.Db.Users.SingleAsync(x => x.Id == f.User);
        const string old = "OldPassword123!", next = "NewPassword456!";
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, old);
        f.Db.AuthSessions.AddRange(new AuthSession { UserId = f.User }, new AuthSession { UserId = f.User });
        await f.Db.SaveChangesAsync();
        var service = new AuthService(f.Db, new ConfigurationBuilder().Build(), f.Access, f.Clock);
        Assert.Equal(
            "CURRENT_PASSWORD",
            (await Assert.ThrowsAsync<AppException>(() => service.ChangePassword(f.User, "wrong", next, next, default))).Code);
        Assert.Equal(
            "PASSWORD_MISMATCH",
            (await Assert.ThrowsAsync<AppException>(() => service.ChangePassword(f.User, old, next, "Different12345", default))).Code);
        Assert.Equal(
            "WEAK_PASSWORD",
            (await Assert.ThrowsAsync<AppException>(() => service.ChangePassword(f.User, old, "short", "short", default))).Code);
        Assert.False(await f.Db.AuthSessions.AnyAsync(x => x.Revoked));
        await service.ChangePassword(f.User, old, next, next, default);
        f.Db.ChangeTracker.Clear();
        user = await f.Db.Users.SingleAsync(x => x.Id == f.User);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(user, user.PasswordHash, next));
        Assert.Equal(PasswordVerificationResult.Failed, new PasswordHasher<AppUser>().VerifyHashedPassword(user, user.PasswordHash, old));
        Assert.False(await f.Db.AuthSessions.AnyAsync(x => !x.Revoked));
        var audit = await f.Db.Audit.SingleAsync();
        Assert.Equal("ChangePassword", audit.Action);
        Assert.DoesNotContain(next, audit.Detail);
    }
}
