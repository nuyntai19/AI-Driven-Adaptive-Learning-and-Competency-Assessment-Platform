using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.API.AssessmentAndReasoning.Background;

public sealed class AIStudentPostProcessingBackgroundService(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<AIStudentPostProcessingBackgroundService> logger) : BackgroundService
{
    private readonly string _owner = $"post-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("AI post-processing scan failed with {ExceptionType}.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task RunOnceAsync(CancellationToken token)
    {
        await using var scanScope = scopes.CreateAsyncScope();
        var db = scanScope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        var centers = await db.Centers.AsNoTracking().Where(x => !x.IsDeleted).Select(x => x.CenterId).ToListAsync(token);
        var tenantFactory = scanScope.ServiceProvider.GetRequiredService<IBackgroundTenantScopeFactory>();
        foreach (var center in centers)
        {
            using var tenant = tenantFactory.BeginScope(center);
            var now = clock.GetUtcNow().UtcDateTime;
            var jobs = await db.AIStudentPostProcessingJobs.AsNoTracking().Where(x => x.CenterId == center &&
                x.Revision > x.ProcessedRevision && x.AvailableAt <= now && (x.LeaseUntil == null || x.LeaseUntil < now))
                .OrderBy(x => x.AvailableAt).Take(5).Select(x => new { x.StudentId, x.SubjectId, x.AssignmentScopeId }).ToListAsync(token);
            foreach (var job in jobs)
            {
                await using var scope = scopes.CreateAsyncScope();
                using var workTenant = scope.ServiceProvider.GetRequiredService<IBackgroundTenantScopeFactory>().BeginScope(center);
                try { await scope.ServiceProvider.GetRequiredService<AIStudentPostProcessor>().RunOneAsync(job.StudentId, job.SubjectId, job.AssignmentScopeId, _owner, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogWarning("AI post-processing job failed with {ExceptionType}.", ex.GetType().Name); }
            }
        }
    }
}
