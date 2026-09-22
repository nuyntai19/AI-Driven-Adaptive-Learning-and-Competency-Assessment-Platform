using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.UseCases;

public interface IUpdateLearningPathSessionUseCase
{
    Task<bool> ExecuteAsync(Guid subjectId, string sessionId, UpdateLearningPathSessionRequest request, CancellationToken cancellationToken);
}

public sealed class UpdateLearningPathSessionUseCase : IUpdateLearningPathSessionUseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedStatuses = ["NotStarted", "InProgress", "Completed", "NeedsReview", "Skipped"];
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public UpdateLearningPathSessionUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<bool> ExecuteAsync(Guid subjectId, string sessionId, UpdateLearningPathSessionRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is not { } centerId || _tenantContext.UserId is not { } studentId ||
            subjectId == Guid.Empty || string.IsNullOrWhiteSpace(sessionId) || !AllowedStatuses.Contains(request.Status, StringComparer.Ordinal))
            return false;
        var path = await _dbContext.LearningPaths
            .Where(p => p.CenterId == centerId && p.StudentId == studentId && p.SubjectId == subjectId && p.Status == LearningPathStatus.Active && !p.IsDeleted)
            .OrderByDescending(p => p.GeneratedAt).FirstOrDefaultAsync(cancellationToken);
        if (path?.PlanJson is null) return false;
        var phases = JsonSerializer.Deserialize<List<LearningPathPhaseDto>>(path.PlanJson.RootElement.GetRawText(), JsonOptions) ?? new();
        var session = phases.SelectMany(p => p.Weeks).SelectMany(w => w.Sessions).SingleOrDefault(s => s.SessionId == sessionId);
        if (session is null) return false;
        session.Status = request.Status;
        foreach (var week in phases.SelectMany(p => p.Weeks))
            week.ProgressPercentage = week.Sessions.Count == 0 ? 0m : Math.Round((decimal)week.Sessions.Count(s => s.Status == "Completed") / week.Sessions.Count * 100m, 1);
        foreach (var phase in phases)
            phase.ProgressPercentage = phase.Weeks.Count == 0 ? 0m : Math.Round(phase.Weeks.Average(w => w.ProgressPercentage), 1);
        path.PlanJson = JsonSerializer.SerializeToDocument(phases, JsonOptions);
        path.UpdatedAt = DateTime.UtcNow;
        path.UpdatedBy = studentId;
        path.RowVersion++;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
