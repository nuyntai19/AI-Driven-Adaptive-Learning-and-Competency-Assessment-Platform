using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public interface IApproveAssignmentResultUseCase
{
    Task<ApproveAssignmentResult> ExecuteAsync(Guid assignmentId, ApproveAssignmentResultRequest request, CancellationToken cancellationToken);
}

public sealed class ApproveAssignmentResult
{
    public TeacherApproveStatus Status { get; private init; }
    public bool IsSuccess => Status == TeacherApproveStatus.Success;
    public AssignmentFinalReviewDto? Data { get; private init; }
    public string ErrorCode { get; private init; } = string.Empty;
    public string ErrorMessage { get; private init; } = string.Empty;

    public static ApproveAssignmentResult Success(AssignmentFinalReviewDto data) => new() { Status = TeacherApproveStatus.Success, Data = data };
    public static ApproveAssignmentResult Fail(TeacherApproveStatus status, string code, string message) => new() { Status = status, ErrorCode = code, ErrorMessage = message };
}

public sealed class ApproveAssignmentResultUseCase : IApproveAssignmentResultUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public ApproveAssignmentResultUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<ApproveAssignmentResult> ExecuteAsync(Guid assignmentId, ApproveAssignmentResultRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is not { } centerId || _tenantContext.UserId is not { } actorId)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Forbidden, "FORBIDDEN", "Không có quyền duyệt kết quả bài tập.");

        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.OrdinalIgnoreCase);
        if (!isTeacher)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Forbidden, "FORBIDDEN", "Chỉ giáo viên phụ trách mới có quyền duyệt kết quả bài tập.");

        if (request.StudentId == Guid.Empty)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.ValidationFailed, "STUDENT_ID_REQUIRED", "Mã học sinh không hợp lệ.");

        if (request.Note?.Length > 1000)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.ValidationFailed, "NOTE_TOO_LONG", "Ghi chú duyệt kết quả không được vượt quá 1000 ký tự.");

        var progress = await _dbContext.StudentAssignmentProgresses
            .Include(p => p.Assignment)
            .ThenInclude(a => a!.Class)
            .SingleOrDefaultAsync(p => p.CenterId == centerId && p.AssignmentId == assignmentId && p.StudentId == request.StudentId && !p.IsDeleted, cancellationToken);

        if (progress?.Assignment?.Class is null)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.NotFound, "ASSIGNMENT_RESULT_NOT_FOUND", "Không tìm thấy kết quả bài tập.");
        if (isTeacher && progress.Assignment.Class.TeacherId != actorId)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Forbidden, "FORBIDDEN", "Bài tập không thuộc lớp giáo viên phụ trách.");
        if (progress.FinalReviewVersion != request.FinalReviewVersion)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Conflict, "CONCURRENCY_CONFLICT", "Kết quả đã được cập nhật. Hãy tải lại trước khi duyệt.");
        if (progress.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Conflict, "ASSIGNMENT_RESULT_LOCKED", "Kết quả đã chốt. Hãy mở lại trước khi thay đổi.");

        var assignmentQuestions = await _dbContext.AssignmentQuestions.AsNoTracking()
            .Where(q => q.CenterId == centerId && q.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);
        var questionIds = assignmentQuestions.Select(q => q.QuestionId).ToList();

        var attempts = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.AssignmentId == assignmentId && a.StudentId == request.StudentId && questionIds.Contains(a.QuestionId))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        var rubric = await AssignmentRubricReviewState.LoadAsync(_dbContext, centerId, assignmentId, request.StudentId, cancellationToken);
        var eligibility = AssignmentFinalReviewPolicy.Evaluate(assignmentQuestions, attempts, rubric.QuestionIds, rubric.GradedAttemptIds);

        if (questionIds.Count == 0 || eligibility.MissingQuestionCount > 0)
        {
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.ValidationFailed, "ASSIGNMENT_INCOMPLETE", "Học sinh chưa hoàn thành tất cả câu hỏi của bài tập.");
        }

        if (eligibility.PendingReviewQuestionCount > 0)
        {
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.ValidationFailed, "ASSIGNMENT_HAS_PENDING_REVIEWS", "Bài tập vẫn còn câu hỏi cần giáo viên rà soát trong hàng đợi. Vui lòng phê duyệt hoặc ghi đè điểm từng câu hỏi trước khi duyệt kết quả toàn bài.");
        }

        if (eligibility.ProcessingQuestionCount > 0 || eligibility.FailedQuestionCount > 0)
        {
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.ValidationFailed, "ASSIGNMENT_NOT_READY", "AI chưa phân tích xong tất cả câu hỏi của bài tập. Vui lòng chờ hoàn tất.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var before = AssignmentFinalReviewWorkflow.Snapshot(progress);
        progress.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Approved;
        progress.FinalReviewedByUserId = actorId;
        progress.FinalReviewedAt = now;
        progress.FinalTeacherNote = request.Note;
        progress.FinalReviewVersion++;
        progress.UpdatedAt = now;
        progress.UpdatedBy = actorId;
        progress.RowVersion++;
        AssignmentFinalReviewWorkflow.Audit(_dbContext, progress, actorId, "AssignmentResultApproved", before,
            request.Note ?? "Giáo viên chốt kết quả toàn bài.", now);
        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Conflict, "CONCURRENCY_CONFLICT", "Kết quả đã thay đổi. Hãy tải lại trước khi chốt.");
        }

        return ApproveAssignmentResult.Success(new AssignmentFinalReviewDto
        {
            AssignmentId = assignmentId.ToString("D"), StudentId = request.StudentId.ToString("D"),
            TeacherFinalReviewStatus = progress.TeacherFinalReviewStatus.ToString(), ReviewedByUserId = actorId.ToString("D"),
            ReviewedAt = now, Note = request.Note, FinalReviewVersion = progress.FinalReviewVersion
        });
    }
}
