using System.Security.Cryptography;
using System.Threading.RateLimiting;
using CheckInApp.Api.Installers;
using CheckInApp.Application;
using CheckInApp.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]))
{
    var directory = Path.Combine(builder.Environment.ContentRootPath, ".local");
    Directory.CreateDirectory(directory);
    var file = Path.Combine(directory, "jwt.key");
    if (!File.Exists(file))
        File.WriteAllText(file, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
    builder.Configuration["Jwt:Key"] = File.ReadAllText(file);
}
if ((builder.Configuration["Jwt:Key"]?.Length ?? 0) < 32) throw new InvalidOperationException("Set Jwt__Key to a random secret of at least 32 characters.");
builder.Services.InsertServiceInAssembly(builder.Configuration, typeof(ServiceInstaller).Assembly);
builder.Services.AddHostedService<CheckInApp.Api.ScheduleWorker>();
builder.Services.AddHostedService<CheckInApp.Api.AvatarCleanupWorker>();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy(
    "auth",
    ctx => RateLimitPartition.GetFixedWindowLimiter(
    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
    _ => new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddDataProtection();
var app = builder.Build();
app.Use(async (ctx, next) => { try { await next(); } catch (AppException e) { ctx.Response.StatusCode = e.Status; await ctx.Response.WriteAsJsonAsync(new { title = e.Message, status = e.Status, code = e.Code, traceId = ctx.TraceIdentifier }); } catch (DbUpdateConcurrencyException) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { title = "Dữ liệu vừa thay đổi. Hãy tải lại.", code = "VERSION_CONFLICT", status = 409 }); } catch (DbUpdateException) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { title = "Dữ liệu trùng hoặc không còn hợp lệ.", code = "DATA_CONFLICT", status = 409 }); } catch (Exception e) { app.Logger.LogError(e, "Request failed: {TraceId}", ctx.TraceIdentifier); ctx.Response.StatusCode = 500; await ctx.Response.WriteAsJsonAsync(new { title = "Không xử lý được yêu cầu.", code = "SERVER_ERROR", status = 500, traceId = ctx.TraceIdentifier }); } });
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet(
    "/api/v1/version",
    () => Results.Ok(new { version = typeof(Program).Assembly.GetName().Version?.ToString(), build = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(Program).Assembly)?.InformationalVersion }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();
    await Bootstrap.Seed(db, app.Configuration, app.Environment.IsDevelopment(), app.Environment.ContentRootPath);
}
app.Run();
public partial class Program;
