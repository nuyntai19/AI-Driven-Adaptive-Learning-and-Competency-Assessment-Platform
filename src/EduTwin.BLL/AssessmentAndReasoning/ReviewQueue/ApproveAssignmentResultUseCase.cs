using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
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
        var isManager = string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.OrdinalIgnoreCase);
        if ((!isTeacher && !isManager) || request.StudentId == Guid.Empty || request.Note?.Length > 1000)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.ValidationFailed, "VALIDATION_FAILED", "Dữ liệu duyệt kết quả không hợp lệ.");

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

        var questionIds = await _dbContext.AssignmentQuestions.AsNoTracking()
            .Where(q => q.CenterId == centerId && q.AssignmentId == assignmentId)
            .Select(q => q.QuestionId)
            .ToListAsync(cancellationToken);
        var attempts = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.AssignmentId == assignmentId && a.StudentId == request.StudentId && questionIds.Contains(a.QuestionId))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        var latest = attempts.GroupBy(a => a.QuestionId).Select(g => g.First()).ToList();
        if (questionIds.Count == 0 || latest.Count != questionIds.Count || latest.Any(a => a.Status is AttemptStatus.PendingAnalysis or AttemptStatus.Processing or AttemptStatus.AnalysisFailed or AttemptStatus.NeedsTeacherReview))
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.ValidationFailed, "ASSIGNMENT_NOT_READY", "AI chưa đánh giá xong tất cả câu hỏi của bài tập.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        progress.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Approved;
        progress.FinalReviewedByUserId = actorId;
        progress.FinalReviewedAt = now;
        progress.FinalTeacherNote = request.Note;
        progress.FinalReviewVersion++;
        progress.UpdatedAt = now;
        progress.UpdatedBy = actorId;
        progress.RowVersion++;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApproveAssignmentResult.Success(new AssignmentFinalReviewDto
        {
            AssignmentId = assignmentId.ToString("D"), StudentId = request.StudentId.ToString("D"),
            TeacherFinalReviewStatus = progress.TeacherFinalReviewStatus.ToString(), ReviewedByUserId = actorId.ToString("D"),
            ReviewedAt = now, Note = request.Note, FinalReviewVersion = progress.FinalReviewVersion
        });
    }
}
