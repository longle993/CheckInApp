using System.Text;
using CheckInApp.Application;
using CheckInApp.Infrastructure;
using CheckInApp.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace CheckInApp.Api.Installers;

public sealed class ServiceInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RedisCache>();
        services.AddScoped<AccessService>();
        services.AddScoped<AuthService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<AdminService>();
        services.AddScoped<ReadService>();
        services.AddScoped<PresenceService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddDbContext<AppDbContext>(options => { if (configuration["Database:Provider"] == "Sqlite") options.UseSqlite(configuration.GetConnectionString("Database")); else options.UseSqlServer(configuration.GetConnectionString("Database")); });
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<CheckInCommand>());
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.MapInboundClaims = false; o.TokenValidationParameters = new() { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)), ValidateIssuer = true, ValidIssuer = configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = configuration["Jwt:Audience"], ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(15) }; o.Events = new() { OnTokenValidated = async context => { var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>(); if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var user) || !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var sid) || !await db.AuthSessions.AnyAsync(x => x.Id == sid && x.UserId == user && !x.Revoked && x.ExpiresAtUtc > DateTime.UtcNow && db.Users.Any(u => u.Id == user && u.Status == Domain.AccountStatus.Active))) context.Fail("Session revoked or account inactive."); } }; });
    }
}
