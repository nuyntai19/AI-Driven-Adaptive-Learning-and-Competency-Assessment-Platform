using System.Globalization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Polling;

public sealed class GetAnalysisJobStatusUseCase : IGetAnalysisJobStatusUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _studentOwnershipGuard;

    public GetAnalysisJobStatusUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard studentOwnershipGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _studentOwnershipGuard = studentOwnershipGuard;
    }

    public async Task<GetAnalysisJobStatusResult> ExecuteAsync(
        string analysisJobId,
        CancellationToken cancellationToken = default)
    {
        if (!HasValidActorContext())
        {
            return GetAnalysisJobStatusResult.NotFound();
        }

        if (!TryParseAnalysisJobId(analysisJobId, out var parsedJobId))
        {
            return GetAnalysisJobStatusResult.ValidationFailed();
        }

        var job = await _dbContext.AIAnalysisJobs
            .AsNoTracking()
            .Where(candidate => candidate.AnalysisJobId == parsedJobId)
            .Select(candidate => new JobStatusSnapshot(
                candidate.AnalysisJobId,
                candidate.AttemptId,
                candidate.Attempt.StudentId,
                candidate.Status,
                candidate.RetryCount,
                candidate.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return GetAnalysisJobStatusResult.NotFound();
        }

        var ownership = await _studentOwnershipGuard.CheckStudentAccessAsync(
            job.StudentId,
            cancellationToken);
        if (ownership == OwnershipDecision.Forbidden)
        {
            return GetAnalysisJobStatusResult.Forbidden();
        }

        if (ownership != OwnershipDecision.Allowed)
        {
            return GetAnalysisJobStatusResult.NotFound();
        }

        var mapping = MapStatus(job.Status, job.AttemptId);
        return GetAnalysisJobStatusResult.Success(new AnalysisJobStatusDataDto
        {
            AnalysisJobId = job.AnalysisJobId.ToString(CultureInfo.InvariantCulture),
            AttemptId = job.AttemptId.ToString(CultureInfo.InvariantCulture),
            Status = job.Status.ToString(),
            RetryCount = job.RetryCount,
            Terminal = mapping.Terminal,
            FeedbackUrl = mapping.FeedbackUrl,
            UpdatedAt = job.UpdatedAt
        });
    }

    private bool HasValidActorContext()
    {
        if (!_tenantContext.IsResolved
            || !_tenantContext.CenterId.HasValue
            || _tenantContext.CenterId.Value == Guid.Empty
            || !_tenantContext.UserId.HasValue
            || _tenantContext.UserId.Value == Guid.Empty
            || string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return false;
        }

        return _tenantContext.Role is nameof(UserRole.Student)
            or nameof(UserRole.Teacher)
            or nameof(UserRole.CenterManager);
    }

    private static bool TryParseAnalysisJobId(
        string? analysisJobId,
        out ulong parsedJobId)
    {
        parsedJobId = 0;
        if (string.IsNullOrEmpty(analysisJobId)
            || analysisJobId.Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        return ulong.TryParse(
                analysisJobId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out parsedJobId)
            && parsedJobId > 0;
    }

    private static StatusMapping MapStatus(AIJobStatus status, ulong attemptId) =>
        status switch
        {
            AIJobStatus.Pending => new(false, null),
            AIJobStatus.Processing => new(false, null),
            AIJobStatus.Completed => new(
                true,
                BuildFeedbackUrl(attemptId)),
            AIJobStatus.FallbackCompleted => new(
                true,
                BuildFeedbackUrl(attemptId)),
            AIJobStatus.FailedTerminal => new(true, null),
            _ => throw new InvalidOperationException(
                $"Unsupported AI job status: {status}")
        };

    private static string BuildFeedbackUrl(ulong attemptId) =>
        $"/api/v1/learning/attempts/{attemptId.ToString(CultureInfo.InvariantCulture)}/feedback";

    private sealed record JobStatusSnapshot(
        ulong AnalysisJobId,
        ulong AttemptId,
        Guid StudentId,
        AIJobStatus Status,
        byte RetryCount,
        DateTime UpdatedAt);

    private sealed record StatusMapping(bool Terminal, string? FeedbackUrl);
}
