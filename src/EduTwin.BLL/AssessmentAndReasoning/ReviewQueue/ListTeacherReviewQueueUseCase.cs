using System.Globalization;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public sealed class ListTeacherReviewQueueUseCase : IListTeacherReviewQueueUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClassOwnershipGuard _classOwnershipGuard;

    public ListTeacherReviewQueueUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IClassOwnershipGuard classOwnershipGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _classOwnershipGuard = classOwnershipGuard;
    }

    public async Task<ListTeacherReviewQueueResult> ExecuteAsync(
        TeacherReviewQueueQuery query,
        CancellationToken cancellationToken)
    {
        if (query is null || query.Page < 1 || query.PageSize is < 1 or > 100)
        {
            return ListTeacherReviewQueueResult.ValidationFailed();
        }

        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId is not { } centerId || centerId == Guid.Empty ||
            _tenantContext.UserId is not { } teacherId || teacherId == Guid.Empty ||
            _tenantContext.Role != nameof(UserRole.Teacher))
        {
            return ListTeacherReviewQueueResult.NotFound();
        }

        if (!await _dbContext.Teachers.AsNoTracking().AnyAsync(
                teacher => teacher.CenterId == centerId && teacher.TeacherId == teacherId,
                cancellationToken))
        {
            return ListTeacherReviewQueueResult.NotFound();
        }

        if (query.ClassId.HasValue)
        {
            if (query.ClassId.Value == Guid.Empty)
            {
                return ListTeacherReviewQueueResult.ValidationFailed();
            }

            var ownership = await _classOwnershipGuard.CheckClassAccessAsync(
                query.ClassId.Value,
                cancellationToken);
            if (ownership == OwnershipDecision.NotFound)
            {
                return ListTeacherReviewQueueResult.NotFound();
            }

            if (ownership != OwnershipDecision.Allowed)
            {
                return ListTeacherReviewQueueResult.Forbidden();
            }
        }

        var reviewItems = _dbContext.EvidenceAssessments
            .AsNoTracking()
            .Where(evidence =>
                evidence.CenterId == centerId &&
                evidence.RequiresTeacherReview &&
                evidence.AnalysisId.HasValue &&
                evidence.Analysis != null &&
                evidence.AnalysisOverrideVersion == evidence.Analysis.OverrideVersion &&
                !_dbContext.EvidenceAssessments.Any(successor =>
                    successor.CenterId == centerId &&
                    successor.SupersedesAssessmentId == evidence.EvidenceAssessmentId) &&
                _dbContext.ClassStudents.Any(membership =>
                    membership.CenterId == centerId &&
                    membership.StudentId == evidence.Attempt.StudentId &&
                    membership.Status == ClassStudentStatus.Active &&
                    membership.Class.TeacherId == teacherId &&
                    (!query.ClassId.HasValue || membership.ClassId == query.ClassId.Value)));

        var totalItems = await reviewItems.LongCountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : checked((int)(1 + ((totalItems - 1) / query.PageSize)));
        var offset = ((long)query.Page - 1) * query.PageSize;

        if (offset > int.MaxValue || totalItems == 0)
        {
            return ListTeacherReviewQueueResult.Success(
                [], query.Page, query.PageSize, totalItems, totalPages);
        }

        var entities = await reviewItems
            .OrderBy(evidence => evidence.EvaluatedAt)
            .ThenBy(evidence => evidence.EvidenceAssessmentId)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Include(evidence => evidence.Analysis)
            .Include(evidence => evidence.Attempt)
                .ThenInclude(attempt => attempt.Student)
            .Include(evidence => evidence.Attempt)
                .ThenInclude(attempt => attempt.Question)
            .ToListAsync(cancellationToken);

        var data = entities.Select(evidence => new TeacherReviewQueueItemDto
        {
            AttemptId = evidence.AttemptId.ToString(CultureInfo.InvariantCulture),
            StudentId = evidence.Attempt.StudentId.ToString("D").ToLowerInvariant(),
            StudentName = evidence.Attempt.Student.FullName,
            QuestionId = evidence.Attempt.QuestionId.ToString(CultureInfo.InvariantCulture),
            QuestionText = evidence.Attempt.Question.QuestionText,
            AnalysisId = evidence.AnalysisId!.Value.ToString(CultureInfo.InvariantCulture),
            FinalAnswer = evidence.Attempt.FinalAnswer,
            ReasoningText = evidence.Attempt.ReasoningText,
            IsFallback = evidence.Analysis!.IsFallback,
            ReasoningQuality = evidence.Analysis.ReasoningQuality,
            AnalysisFeedback = evidence.Analysis!.Feedback,
            AnalysisConfidence = evidence.Analysis.AnalysisConfidence,
            Evidence = EvidenceProjectionMapper.Map(evidence),
            SubmittedAt = NormalizeUtc(evidence.Attempt.CreatedAt)
        }).ToList();

        return ListTeacherReviewQueueResult.Success(
            data, query.Page, query.PageSize, totalItems, totalPages);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
