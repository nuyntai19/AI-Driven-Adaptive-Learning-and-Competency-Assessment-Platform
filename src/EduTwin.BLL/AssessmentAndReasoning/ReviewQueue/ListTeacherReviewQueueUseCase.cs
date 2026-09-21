using System.Globalization;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
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
            _tenantContext.UserId is not { } actorId || actorId == Guid.Empty)
        {
            return ListTeacherReviewQueueResult.NotFound();
        }

        var isTeacher = _tenantContext.Role == nameof(UserRole.Teacher);
        var isCenterManager = _tenantContext.Role == nameof(UserRole.CenterManager);
        if (!isTeacher && !isCenterManager)
        {
            return ListTeacherReviewQueueResult.NotFound();
        }

        if (isTeacher && !await _dbContext.Teachers.AsNoTracking().AnyAsync(
                teacher => teacher.CenterId == centerId && teacher.TeacherId == actorId,
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
                (evidence.RequiresTeacherReview || _dbContext.StudentAssignmentProgresses.Any(progress =>
                    progress.CenterId == centerId &&
                    progress.AssignmentId == evidence.Attempt.AssignmentId &&
                    progress.StudentId == evidence.Attempt.StudentId &&
                    progress.Status == ProgressStatus.Completed &&
                    progress.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Pending &&
                    !progress.IsDeleted)) &&
                evidence.AnalysisId.HasValue &&
                evidence.Analysis != null &&
                evidence.AnalysisOverrideVersion == evidence.Analysis.OverrideVersion &&
                !_dbContext.EvidenceAssessments.Any(successor =>
                    successor.CenterId == centerId &&
                    successor.SupersedesAssessmentId == evidence.EvidenceAssessmentId) &&
                evidence.Attempt.AssignmentId.HasValue &&
                _dbContext.AssignmentTargets.Any(target =>
                    target.CenterId == centerId &&
                    target.AssignmentId == evidence.Attempt.AssignmentId.Value &&
                    target.StudentId == evidence.Attempt.StudentId &&
                    target.Assignment != null &&
                    target.Assignment.Class != null &&
                    (!isTeacher || target.Assignment.Class.TeacherId == actorId) &&
                    (!query.ClassId.HasValue || target.Assignment.ClassId == query.ClassId.Value)));

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

        var attemptIds = entities.Select(e => e.AttemptId).Distinct().ToList();
        var reviewRequests = attemptIds.Count > 0
            ? await _dbContext.StudentReviewRequests
                .AsNoTracking()
                .Where(r => r.CenterId == centerId && attemptIds.Contains(r.AttemptId) && r.Status == StudentReviewRequestStatus.Pending)
                .ToDictionaryAsync(r => r.AttemptId, cancellationToken)
            : new Dictionary<ulong, StudentReviewRequest>();

        var assignmentIds = entities.Select(e => e.Attempt.AssignmentId!.Value).Distinct().ToList();
        var studentIds = entities.Select(e => e.Attempt.StudentId).Distinct().ToList();
        var progressRows = await _dbContext.StudentAssignmentProgresses
            .AsNoTracking()
            .Include(p => p.Assignment)
            .Where(p => p.CenterId == centerId && assignmentIds.Contains(p.AssignmentId) && studentIds.Contains(p.StudentId) && !p.IsDeleted)
            .ToListAsync(cancellationToken);
        var progressByAssignmentStudent = progressRows.ToDictionary(p => (p.AssignmentId, p.StudentId));

        var data = entities.Select(evidence =>
        {
            var hasRequest = reviewRequests.TryGetValue(evidence.AttemptId, out var req);
            progressByAssignmentStudent.TryGetValue((evidence.Attempt.AssignmentId!.Value, evidence.Attempt.StudentId), out var progress);
            return new TeacherReviewQueueItemDto
            {
                AttemptId = evidence.AttemptId.ToString(CultureInfo.InvariantCulture),
                AssignmentId = evidence.Attempt.AssignmentId.Value.ToString("D"),
                AssignmentTitle = progress?.Assignment?.Title ?? "Bài tập",
                StudentId = evidence.Attempt.StudentId.ToString("D").ToLowerInvariant(),
                StudentName = evidence.Attempt.Student.FullName,
                QuestionId = evidence.Attempt.QuestionId.ToString(CultureInfo.InvariantCulture),
                SubjectId = evidence.Attempt.Question.SubjectId.ToString("D").ToLowerInvariant(),
                QuestionText = evidence.Attempt.Question.QuestionText,
                AnalysisId = evidence.AnalysisId!.Value.ToString(CultureInfo.InvariantCulture),
                FinalAnswer = evidence.Attempt.FinalAnswer,
                ReasoningText = evidence.Attempt.ReasoningText,
                IsFallback = evidence.Analysis!.IsFallback,
                ReasoningQuality = evidence.Analysis.ReasoningQuality,
                AnalysisFeedback = evidence.Analysis!.Feedback,
                AnalysisConfidence = evidence.Analysis.AnalysisConfidence,
                Evidence = EvidenceProjectionMapper.Map(evidence),
                SubmittedAt = NormalizeUtc(evidence.Attempt.CreatedAt),
                HasStudentReviewRequest = hasRequest,
                StudentReviewReason = hasRequest ? req!.StudentComment : null,
                TeacherFinalReviewStatus = progress?.TeacherFinalReviewStatus.ToString() ?? "Pending",
                FinalReviewVersion = progress?.FinalReviewVersion ?? 0
            };
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
