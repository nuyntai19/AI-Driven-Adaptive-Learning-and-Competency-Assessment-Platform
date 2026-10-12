using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Platform.Operations;
using EduTwin.BLL.Seeding;
using EduTwin.DAL.PlatformOperations;
using Microsoft.Extensions.Options;

namespace EduTwin.API.PlatformOperations;

public sealed class OperationsBackgroundService(IServiceScopeFactory scopes, OperationalTelemetry telemetry,
    IOptions<PlatformOperationsOptions> options, TimeProvider clock, ILogger<OperationsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextStorage = DateTime.MinValue; var nextAlerts = DateTime.MinValue;
        var pending = new List<PlatformAiCall>(); var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            while (pending.Count < 100 && telemetry.Calls.Reader.TryRead(out var call)) pending.Add(call);
            if (pending.Count > 0)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<OperationsMaintenance>().StoreCallsAsync(pending, stoppingToken);
                    pending.Clear(); failures = 0;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.LogWarning("Operational call metadata persistence failed: {ErrorType}", ex.GetType().Name);
                    if (++failures >= 10) { telemetry.Drop(pending.Count); pending.Clear(); failures = 0; }
                }
            }
            var now = clock.GetUtcNow().UtcDateTime;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var tenant = scope.ServiceProvider.GetRequiredService<IBackgroundTenantScopeFactory>().BeginScope(AuthorizationBootstrapper.ReservedPlatformCenterId);
                var maintenance = scope.ServiceProvider.GetRequiredService<OperationsMaintenance>();
                if (now >= nextStorage)
                {
                    nextStorage = now.AddMinutes(1); // Failed collection backs off instead of scanning every two seconds.
                    await maintenance.CaptureStorageAsync(stoppingToken);
                    nextStorage = now.AddMinutes(Math.Clamp(options.Value.SnapshotMinutes, 1, 60));
                }
                if (now >= nextAlerts)
                { nextAlerts = now.AddSeconds(30); await maintenance.EvaluateAlertsAsync(stoppingToken); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Operational snapshot collection failed: {ErrorType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
        // Shutdown is bounded; remaining volatile metadata is explicitly counted as incomplete.
        telemetry.Drop(pending.Count + telemetry.Calls.Reader.Count);
    }
}
