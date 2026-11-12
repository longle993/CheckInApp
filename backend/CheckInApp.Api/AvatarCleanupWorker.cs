using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;
// Files are queued transactionally by deletion and removed only after commit. Failed I/O retries.
public sealed class AvatarCleanupWorker(
    IServiceScopeFactory scopes,
    IWebHostEnvironment env,
    ILogger<AvatarCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                foreach (var job in await db.Audit.Where(x => x.Action == "AvatarCleanupPending").Take(100).ToListAsync(ct))
                {
                    if (job.Detail.EndsWith(".png", StringComparison.Ordinal)
                        && Guid.TryParseExact(Path.GetFileNameWithoutExtension(job.Detail), "N", out _)
                        && Path.GetFileName(job.Detail) == job.Detail)
                    {
                        var owners = await db.Audit.Where(x => x.Action == "Avatar" && x.Detail == job.Detail).Select(x => new { x.EntityId, x.EntityType }).ToListAsync(ct);
                        var shared = false;
                        foreach (var owner in owners)
                            if (owner.EntityType == "students"
                                ? await db.Students.AnyAsync(x => x.Id == owner.EntityId, ct)
                                : await db.Catechists.AnyAsync(x => x.Id == owner.EntityId, ct))
                            {
                                shared = true;
                                break;
                            }
                        if (shared)
                            continue;
                        File.Delete(Path.Combine(env.ContentRootPath, ".local", "avatars", job.Detail));
                    }
                    job.Action = "AvatarCleanupComplete";
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning(e, "Avatar cleanup will retry"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
