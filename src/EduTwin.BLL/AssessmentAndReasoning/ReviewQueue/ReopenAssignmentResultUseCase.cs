using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public interface IReopenAssignmentResultUseCase
{
    Task<ApproveAssignmentResult> ExecuteAsync(Guid assignmentId, ReopenAssignmentResultRequest request, CancellationToken token);
}

public sealed class ReopenAssignmentResultUseCase(EduTwinDbContext db, ITenantContext tenant, TimeProvider timeProvider)
    : IReopenAssignmentResultUseCase
{
    public async Task<ApproveAssignmentResult> ExecuteAsync(Guid assignmentId, ReopenAssignmentResultRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!tenant.IsResolved || tenant.CenterId is not { } centerId || tenant.UserId is not { } actorId ||
            tenant.Role != nameof(UserRole.Teacher))
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Forbidden, "FORBIDDEN", "Chỉ giáo viên phụ trách mới có quyền mở lại kết quả.");
        if (assignmentId == Guid.Empty || request.StudentId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length is < 5 or > 1000)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.ValidationFailed, "REOPEN_REASON_REQUIRED", "Lý do mở lại phải từ 5 đến 1000 ký tự.");
        var progress = await db.StudentAssignmentProgresses.Include(p => p.Assignment).ThenInclude(a => a!.Class)
            .SingleOrDefaultAsync(p => p.CenterId == centerId && p.AssignmentId == assignmentId &&
                p.StudentId == request.StudentId && !p.IsDeleted, token);
        if (progress?.Assignment?.Class is null || progress.Assignment.IsDeleted)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.NotFound, "ASSIGNMENT_RESULT_NOT_FOUND", "Không tìm thấy kết quả bài tập.");
        if (progress.Assignment.Class.TeacherId != actorId)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Forbidden, "FORBIDDEN", "Bài tập không thuộc lớp giáo viên phụ trách.");
        if (progress.FinalReviewVersion != request.FinalReviewVersion || progress.TeacherFinalReviewStatus != TeacherFinalReviewStatus.Approved)
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Conflict, "CONCURRENCY_CONFLICT", "Kết quả đã thay đổi hoặc đã được mở lại. Hãy tải lại.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var before = AssignmentFinalReviewWorkflow.Snapshot(progress);
        AssignmentFinalReviewWorkflow.Reopen(progress, actorId, now);
        AssignmentFinalReviewWorkflow.Audit(db, progress, actorId, "AssignmentResultReopened", before, request.Reason.Trim(), now);
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        {
            return ApproveAssignmentResult.Fail(TeacherApproveStatus.Conflict, "CONCURRENCY_CONFLICT", "Kết quả đã thay đổi. Hãy tải lại trước khi mở lại.");
        }
        return ApproveAssignmentResult.Success(new AssignmentFinalReviewDto
        {
            AssignmentId = assignmentId.ToString("D"), StudentId = request.StudentId.ToString("D"),
            TeacherFinalReviewStatus = "Pending", FinalReviewVersion = progress.FinalReviewVersion,
            ReviewedByUserId = actorId.ToString("D"), ReviewedAt = now, Note = request.Reason.Trim()
        });
    }
}
