using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewRequest;

public sealed class CreateStudentReviewRequestUseCase : ICreateStudentReviewRequestUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateStudentReviewRequestUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<CreateStudentReviewRequestResult> ExecuteAsync(
        ulong attemptId,
        CreateStudentReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (attemptId == 0)
        {
            return CreateStudentReviewRequestResult.NotFound();
        }

        var comment = !string.IsNullOrWhiteSpace(request.StudentComment) ? request.StudentComment : request.Reason;
        if (string.IsNullOrWhiteSpace(comment))
        {
            return CreateStudentReviewRequestResult.ValidationFailed("Lý do yêu cầu xem xét không được để trống.");
        }

        if (comment.Trim().Length > 2000)
        {
            return CreateStudentReviewRequestResult.ValidationFailed("Lý do yêu cầu xem xét không được vượt quá 2000 ký tự.");
        }

        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty)
        {
            return CreateStudentReviewRequestResult.NotFound();
        }

        var centerId = _tenantContext.CenterId.Value;
        var currentUserId = _tenantContext.UserId.Value;
        var currentUserRole = _tenantContext.Role;

        var attempt = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.AttemptId == attemptId)
            .FirstOrDefaultAsync(cancellationToken);

        if (attempt == null)
        {
            return CreateStudentReviewRequestResult.NotFound();
        }

        if (currentUserRole == nameof(UserRole.Student) && attempt.StudentId != currentUserId)
        {
            return CreateStudentReviewRequestResult.Forbidden();
        }

        // Idempotency: check if pending review request exists
        var existingRequest = await _dbContext.StudentReviewRequests
            .Where(r => r.CenterId == centerId && r.AttemptId == attemptId && r.Status == StudentReviewRequestStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingRequest != null)
        {
            return CreateStudentReviewRequestResult.Success(Map(existingRequest));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var reviewRequest = new StudentReviewRequest
        {
            CenterId = centerId,
            AttemptId = attemptId,
            StudentId = attempt.StudentId,
            QuestionId = attempt.QuestionId,
            StudentComment = comment.Trim(),
            Status = StudentReviewRequestStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.StudentReviewRequests.Add(reviewRequest);

        // Flag analysis and evidence as requiring teacher review
        var analysis = await _dbContext.ReasoningAnalyses
            .Where(ra => ra.CenterId == centerId && ra.AttemptId == attemptId)
            .FirstOrDefaultAsync(cancellationToken);

        if (analysis != null)
        {
            analysis.NeedsTeacherReview = true;
            analysis.UpdatedAt = now;
        }

        var evidence = await _dbContext.EvidenceAssessments
            .Where(ea => ea.CenterId == centerId && ea.AttemptId == attemptId)
            .OrderByDescending(ea => ea.EvidenceAssessmentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (evidence != null)
        {
            evidence.RequiresTeacherReview = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreateStudentReviewRequestResult.Success(Map(reviewRequest));
    }

    private static StudentReviewRequestDto Map(StudentReviewRequest request) =>
        new()
        {
            RequestId = request.RequestId,
            AttemptId = request.AttemptId,
            StudentId = request.StudentId,
            QuestionId = request.QuestionId,
            StudentComment = request.StudentComment,
            Status = request.Status,
            TeacherNote = request.TeacherNote,
            ResolvedByTeacherId = request.ResolvedByTeacherId,
            ResolvedAt = request.ResolvedAt,
            CreatedAt = request.CreatedAt
        };
}
