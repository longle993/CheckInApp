using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Api;

public sealed class ScheduleWorker(IServiceScopeFactory scopes, ILogger<ScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<ScheduleService>();
                foreach (var id in await db.Schedules.Select(s => s.ClassRoomId).ToListAsync(stoppingToken))
                    await service.Synchronize(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError(e, "Session schedule synchronization failed; will retry next cycle."); }
    }
}
