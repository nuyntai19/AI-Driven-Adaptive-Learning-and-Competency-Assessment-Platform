using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public sealed class VoidAssignmentQuestionUseCase : IVoidAssignmentQuestionUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public VoidAssignmentQuestionUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<VoidAssignmentQuestionResult> ExecuteAsync(
        Guid assignmentId,
        ulong questionId,
        VoidAssignmentQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (assignmentId == Guid.Empty || questionId == 0)
        {
            return VoidAssignmentQuestionResult.Fail(
                VoidAssignmentQuestionStatus.ValidationFailed,
                "VALIDATION_FAILED",
                "Mã bài tập hoặc mã câu hỏi không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request?.VoidReason) || request.VoidReason.Trim().Length < 5)
        {
            return VoidAssignmentQuestionResult.Fail(
                VoidAssignmentQuestionStatus.ValidationFailed,
                "VALIDATION_FAILED",
                "Lý do hủy câu phải có ít nhất 5 ký tự.");
        }

        if (request.VoidReason.Trim().Length > 1000)
        {
            return VoidAssignmentQuestionResult.Fail(
                VoidAssignmentQuestionStatus.ValidationFailed,
                "VALIDATION_FAILED",
                "Lý do hủy câu không được vượt quá 1000 ký tự.");
        }

        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            !_tenantContext.UserId.HasValue)
        {
            return VoidAssignmentQuestionResult.Fail(
                VoidAssignmentQuestionStatus.Forbidden,
                "FORBIDDEN",
                "Không có quyền thao tác trong hệ thống.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var role = _tenantContext.Role ?? string.Empty;

        var isTeacher = string.Equals(role, nameof(UserRole.Teacher), StringComparison.OrdinalIgnoreCase);

        if (!isTeacher)
        {
            return VoidAssignmentQuestionResult.Fail(
                VoidAssignmentQuestionStatus.Forbidden,
                "FORBIDDEN",
                "Chỉ giáo viên phụ trách mới có quyền hủy câu hỏi trong bài tập.");
        }

        // 1. Load assignment with class
        var assignment = await _dbContext.Assignments
            .Include(a => a.Class)
            .SingleOrDefaultAsync(
                a => a.CenterId == centerId && a.AssignmentId == assignmentId && !a.IsDeleted,
                cancellationToken);

        if (assignment is null)
        {
            return VoidAssignmentQuestionResult.Fail(
                VoidAssignmentQuestionStatus.NotFound,
                "ASSIGNMENT_NOT_FOUND",
                "Không tìm thấy bài tập.");
        }

        // Teacher ownership guard
        if (isTeacher)
        {
            var isClassTeacher = assignment.Class != null && assignment.Class.TeacherId == actorId;
            var isAuthor = assignment.CreatedByTeacherId == actorId;
            if (!isClassTeacher && !isAuthor)
            {
                return VoidAssignmentQuestionResult.Fail(
                    VoidAssignmentQuestionStatus.Forbidden,
                    "FORBIDDEN",
                    "Bạn không có quyền quản lý bài tập của lớp học này.");
            }
        }

        // 2. Verify question is in this assignment
        var assignmentQuestion = await _dbContext.AssignmentQuestions
            .SingleOrDefaultAsync(
                aq => aq.CenterId == centerId && aq.AssignmentId == assignmentId && aq.QuestionId == questionId,
                cancellationToken);

        if (assignmentQuestion is null)
        {
            return VoidAssignmentQuestionResult.Fail(
                VoidAssignmentQuestionStatus.NotFound,
                "QUESTION_NOT_IN_ASSIGNMENT",
                "Câu hỏi không nằm trong bài tập này.");
        }

        // 3. Load question
        var question = await _dbContext.Questions
            .SingleOrDefaultAsync(
                q => q.CenterId == centerId && q.QuestionId == questionId && !q.IsDeleted,
                cancellationToken);

        if (question is null)
        {
            return VoidAssignmentQuestionResult.Fail(
                VoidAssignmentQuestionStatus.NotFound,
                "QUESTION_NOT_FOUND",
                "Không tìm thấy câu hỏi trong hệ thống.");
        }

        if (assignmentQuestion.IsVoided)
            return VoidAssignmentQuestionResult.Fail(VoidAssignmentQuestionStatus.Conflict, "QUESTION_ALREADY_VOIDED", "Câu hỏi đã được hủy trong bài tập này.");
        // Load every affected progress row before any mutation. RowVersion protects
        // against a concurrent final approval; SaveChanges commits all rows atomically.
        var affectedProgresses = await _dbContext.StudentAssignmentProgresses
            .Where(p => p.CenterId == centerId && p.AssignmentId == assignmentId && !p.IsDeleted)
            .ToListAsync(cancellationToken);
        if (affectedProgresses.Any(p => p.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved) && !request.ReopenFinalizedResults)
            return VoidAssignmentQuestionResult.Fail(VoidAssignmentQuestionStatus.Conflict, "FINAL_RESULTS_REQUIRE_REOPEN_ACKNOWLEDGEMENT",
                "Hủy câu ảnh hưởng tất cả học sinh trong bài tập. Hãy xác nhận mở lại các kết quả đã chốt trước khi tiếp tục.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var progress in affectedProgresses)
        {
            var before = AssignmentFinalReviewWorkflow.Snapshot(progress);
            AssignmentFinalReviewWorkflow.Reopen(progress, actorId, now);
            AssignmentFinalReviewWorkflow.Audit(_dbContext, progress, actorId, "AssignmentQuestionVoided", before,
                request.VoidReason.Trim(), now);
        }

        // 4. Mark question as voided specifically in this assignment
        assignmentQuestion.IsVoided = true;
        assignmentQuestion.VoidReason = request.VoidReason.Trim();
        assignmentQuestion.VoidedAt = now;
        assignmentQuestion.VoidedByUserId = actorId;

        // 5. Quarantine question in bank if requested
        if (request.ArchiveQuestionInBank)
        {
            if (question.Status != QuestionStatus.Archived)
            {
                question.Status = QuestionStatus.Archived;
                question.UpdatedAt = now;
                question.UpdatedBy = actorId;
                question.RowVersion++;
            }
        }

        // Points to award
        // Attempt scores use the question's internal scale, not assignment weight.
        var fullScore = question.MaxScore;

        // 6. Load all attempts for this question in this assignment
        var attempts = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.AssignmentId == assignmentId && a.QuestionId == questionId)
            .ToListAsync(cancellationToken);

        var attemptIds = attempts.Select(a => a.AttemptId).ToList();
        var analyses = attemptIds.Count > 0
            ? await _dbContext.ReasoningAnalyses
                .Where(ra => ra.CenterId == centerId && attemptIds.Contains(ra.AttemptId))
                .ToListAsync(cancellationToken)
            : new List<ReasoningAnalysis>();

        var analysisByAttemptId = analyses.ToDictionary(a => a.AttemptId);

        foreach (var attempt in attempts)
        {
            attempt.AwardedScore = fullScore;
            attempt.IsCorrect = true;
            attempt.Status = AttemptStatus.Completed;
            attempt.UpdatedAt = now;
            attempt.RowVersion++;

            if (analysisByAttemptId.TryGetValue(attempt.AttemptId, out var analysis))
            {
                analysis.OverrideAwardedScore = fullScore;
                analysis.OverrideIsCorrect = true;
                analysis.OverrideReason = $"[HỦY CÂU - ĐỀ SAI]: {request.VoidReason.Trim()}";
                analysis.OverriddenByUserId = actorId;
                analysis.OverriddenAt = now;
                analysis.NeedsTeacherReview = false;
                analysis.OverrideVersion++;
                analysis.UpdatedAt = now;
                analysis.RowVersion++;
            }
        }

        // 6. Resolve pending student review requests for these attempts
        if (attemptIds.Count > 0)
        {
            var pendingReviewRequests = await _dbContext.StudentReviewRequests
                .Where(r => r.CenterId == centerId && attemptIds.Contains(r.AttemptId) && r.Status == StudentReviewRequestStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var req in pendingReviewRequests)
            {
                req.Status = StudentReviewRequestStatus.Resolved;
                req.TeacherNote = $"Giáo viên đã xác nhận câu hỏi có sự cố và tính trọn điểm cả lớp: {request.VoidReason.Trim()}";
                req.ResolvedByTeacherId = actorId;
                req.ResolvedAt = now;
                req.UpdatedAt = now;
                req.RowVersion++;
            }

            // 7. Append zero-weight EvidenceAssessment to isolate and protect Digital Twin
            foreach (var attempt in attempts)
            {
                var previousEvidence = await _dbContext.EvidenceAssessments
                    .Where(e => e.CenterId == centerId && e.AttemptId == attempt.AttemptId)
                    .OrderByDescending(e => e.EvidenceAssessmentId)
                    .FirstOrDefaultAsync(cancellationToken);

                var reasonDoc = JsonSerializer.SerializeToDocument(new[] { "QUESTION_VOIDED_BY_TEACHER" });
                analysisByAttemptId.TryGetValue(attempt.AttemptId, out var analysis);

                var voidEvidence = new EvidenceAssessment
                {
                    CenterId = centerId,
                    AttemptId = attempt.AttemptId,
                    AnalysisId = analysis?.AnalysisId ?? previousEvidence?.AnalysisId,
                    SupersedesAssessmentId = previousEvidence?.EvidenceAssessmentId,
                    SourceType = EvidenceSourceType.TeacherOverride,
                    TrustLevel = EvidenceTrustLevel.ReviewOnly,
                    DecisionMode = EvidenceDecisionMode.DeterministicOnly,
                    ReasoningWeight = 0m,
                    ReasonCodes = reasonDoc,
                    RequiresTeacherReview = false,
                    PolicyVersion = previousEvidence?.PolicyVersion ?? "policy-v1",
                    AnalysisOverrideVersion = (analysis?.OverrideVersion ?? 0),
                    EvaluatedAt = now,
                    CreatedAt = now,
                    CreatedBy = actorId
                };

                _dbContext.EvidenceAssessments.Add(voidEvidence);
            }
        }

        // 8. Unblock in-progress student progress if voiding this question completes the assignment
        var inProgressList = await _dbContext.StudentAssignmentProgresses
            .Where(p => p.CenterId == centerId && p.AssignmentId == assignmentId && p.Status == ProgressStatus.InProgress && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        if (inProgressList.Count > 0)
        {
            var assignmentQuestions = await _dbContext.AssignmentQuestions.AsNoTracking()
                .Where(aq => aq.CenterId == centerId && aq.AssignmentId == assignmentId)
                .ToListAsync(cancellationToken);
            var allVoidedIds = assignmentQuestions.Where(aq => aq.IsVoided || aq.QuestionId == questionId).Select(aq => aq.QuestionId).ToHashSet();

            foreach (var p in inProgressList)
            {
                var studentAttemptQuestionIds = await _dbContext.Attempts.AsNoTracking()
                    .Where(a => a.CenterId == centerId && a.AssignmentId == assignmentId && a.StudentId == p.StudentId)
                    .Select(a => a.QuestionId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var resolvedCount = studentAttemptQuestionIds.Concat(allVoidedIds).Distinct().Count();
                if (resolvedCount >= p.TotalQuestionCount && p.TotalQuestionCount > 0)
                {
                    p.Status = ProgressStatus.Completed;
                    p.CompletedAt ??= now;
                    p.UpdatedAt = now;
                    p.UpdatedBy = actorId;
                }
            }
        }

        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return VoidAssignmentQuestionResult.Fail(VoidAssignmentQuestionStatus.Conflict, "CONCURRENCY_CONFLICT",
                "Kết quả bài tập đã thay đổi. Hãy tải lại và xác nhận phạm vi ảnh hưởng trước khi hủy câu.");
        }

        return VoidAssignmentQuestionResult.Success(new VoidAssignmentQuestionResultDto
        {
            AssignmentId = assignmentId.ToString("D"),
            QuestionId = questionId.ToString(CultureInfo.InvariantCulture),
            VoidedAttemptsCount = attempts.Count,
            QuestionArchived = question.Status == QuestionStatus.Archived,
            Message = $"Đã hủy câu hỏi #{questionId} trong bài tập này. Tất cả học sinh được tính trọn điểm đóng góp của câu (điểm câu: 10/10). Các kết quả đã chốt bị ảnh hưởng cần được chốt lại."
        });
    }
}
