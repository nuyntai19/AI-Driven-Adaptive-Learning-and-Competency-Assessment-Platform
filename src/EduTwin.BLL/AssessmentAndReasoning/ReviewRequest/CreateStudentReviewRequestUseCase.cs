using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
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

        var rawComment = !string.IsNullOrWhiteSpace(request.StudentComment) ? request.StudentComment : request.Reason;
        if (string.IsNullOrWhiteSpace(rawComment))
        {
            return CreateStudentReviewRequestResult.ValidationFailed("Lý do yêu cầu xem xét không được để trống.");
        }

        var category = request.DisputeCategory?.Trim().ToUpperInvariant();
        string[] categories = ["DEFECTIVE_QUESTION", "QUESTION_TYPO", "DEFECTIVE_QUESTION_WRONG_CONTENT",
            "DEFECTIVE_QUESTION_WRONG_OPTIONS", "DEFECTIVE_QUESTION_TYPO_LATEX", "DEFECTIVE_QUESTION_OTHER"];
        if (!string.IsNullOrEmpty(category) && !categories.Contains(category))
        {
            return CreateStudentReviewRequestResult.ValidationFailed("Loại báo cáo đề bài không hợp lệ.");
        }
        // Keep older clients which sent a category prefix without the DTO field compatible.
        var isQuestionReport = !string.IsNullOrEmpty(category) || categories.Any(value =>
            rawComment.StartsWith($"[{value}]", StringComparison.OrdinalIgnoreCase));
        var comment = !string.IsNullOrEmpty(category) && !rawComment.StartsWith($"[{category}]", StringComparison.OrdinalIgnoreCase)
            ? $"[{category}] {rawComment.Trim()}"
            : rawComment.Trim();

        if (comment.Length > 1000)
        {
            return CreateStudentReviewRequestResult.ValidationFailed("Lý do yêu cầu xem xét không được vượt quá 1000 ký tự.");
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

        var analysis = await _dbContext.ReasoningAnalyses
            .FirstOrDefaultAsync(ra => ra.CenterId == centerId && ra.AttemptId == attemptId, cancellationToken);
        var assignmentApproved = attempt.AssignmentId.HasValue &&
            await _dbContext.StudentAssignmentProgresses.AnyAsync(p =>
                p.CenterId == centerId && p.AssignmentId == attempt.AssignmentId &&
                p.StudentId == attempt.StudentId && !p.IsDeleted &&
                p.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved, cancellationToken);
        var voided = attempt.AssignmentId.HasValue &&
            await _dbContext.AssignmentQuestions.AnyAsync(q =>
                q.CenterId == centerId && q.AssignmentId == attempt.AssignmentId &&
                q.QuestionId == attempt.QuestionId && q.IsVoided, cancellationToken);
        var allowed = isQuestionReport
            ? AttemptFeedbackActionPolicy.CanReportQuestion(attempt, false, voided)
            : AttemptFeedbackActionPolicy.CanRequestReview(attempt, analysis, assignmentApproved, false, voided);
        if (!allowed)
        {
            return CreateStudentReviewRequestResult.ValidationFailed(isQuestionReport
                ? "Không thể báo cáo câu đã hủy hoặc bài đang phân tích. Vui lòng đợi kết quả."
                : "Chỉ có thể yêu cầu xem xét lại sau khi giáo viên đã chấm hoặc duyệt kết quả.");
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
            CreatedBy = currentUserId,
            UpdatedAt = now
        };

        _dbContext.StudentReviewRequests.Add(reviewRequest);

        // Flag the mutable analysis/attempt as requiring teacher review.
        if (analysis != null)
        {
            analysis.NeedsTeacherReview = true;
            analysis.UpdatedAt = now;
        }

        attempt.Status = AttemptStatus.NeedsTeacherReview;
        attempt.UpdatedAt = now;

        var evidence = await _dbContext.EvidenceAssessments
            .Where(ea => ea.CenterId == centerId && ea.AttemptId == attemptId)
            .OrderByDescending(ea => ea.EvidenceAssessmentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (evidence != null && !evidence.RequiresTeacherReview)
        {
            // Evidence is append-only by database contract. Create a successor
            // instead of updating the existing row (which the MySQL trigger
            // correctly rejects).
            var reasonCodes = ReadReasonCodes(evidence.ReasonCodes);
            reasonCodes.Add(EvidenceReasonCodes.StudentReviewRequested);

            _dbContext.EvidenceAssessments.Add(new EvidenceAssessment
            {
                CenterId = evidence.CenterId,
                AttemptId = evidence.AttemptId,
                AnalysisId = evidence.AnalysisId,
                SupersedesAssessmentId = evidence.EvidenceAssessmentId,
                SourceType = evidence.SourceType,
                TrustLevel = evidence.TrustLevel,
                DecisionMode = evidence.DecisionMode,
                ReasoningWeight = evidence.ReasoningWeight,
                ReasonCodes = JsonSerializer.SerializeToDocument(reasonCodes.Distinct().ToArray()),
                RequiresTeacherReview = true,
                PolicyVersion = evidence.PolicyVersion,
                AnalysisOverrideVersion = evidence.AnalysisOverrideVersion,
                EvaluatedAt = now,
                CreatedAt = now,
                CreatedBy = currentUserId
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return CreateStudentReviewRequestResult.ValidationFailed("Kết quả vừa thay đổi. Vui lòng tải lại trước khi gửi yêu cầu.");
        }

        return CreateStudentReviewRequestResult.Success(Map(reviewRequest));
    }

    private static List<string> ReadReasonCodes(JsonDocument? document)
    {
        if (document?.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return document.RootElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
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
