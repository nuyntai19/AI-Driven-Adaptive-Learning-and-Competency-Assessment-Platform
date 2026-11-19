using System.Data;
using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Assignments;

public sealed class PublishExamResultsUseCase(EduTwinDbContext db, ITenantContext tenant, TimeProvider clock)
{
    public async Task<(int Status, string? Error, DateTime? PublishedAt)> ExecuteAsync(
        Guid assignmentId, string traceId, CancellationToken ct)
    {
        if (!tenant.IsResolved || tenant.CenterId is not { } center || tenant.UserId is not { } actor ||
            tenant.Role != nameof(UserRole.Teacher))
            return (403, "Chỉ giáo viên phụ trách mới có quyền công bố điểm.", null);

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var assignment = await db.Assignments.Include(a => a.Class)
            .SingleOrDefaultAsync(a => a.CenterId == center && a.AssignmentId == assignmentId, ct);
        if (assignment?.Class is null || assignment.Class.TeacherId != actor)
            return (404, "Không tìm thấy kỳ thi trong lớp phụ trách.", null);
        if (!ExamResultVisibility.IsTermExam(assignment.Kind) || assignment.Status == AssignmentStatus.Draft)
            return (400, "Chỉ công bố theo đợt đối với bài thi giữa kỳ và cuối kỳ đã giao.", null);
        if (assignment.ResultsPublishedAt is { } publishedAt)
            return (200, null, publishedAt);

        var now = clock.GetUtcNow().UtcDateTime;
        if (assignment.ExamAt > now || assignment.Status != AssignmentStatus.Closed &&
            (assignment.DueAt == null || assignment.DueAt > now))
            return (409, "Kỳ thi phải kết thúc trước khi công bố.", null);

        var progress = db.StudentAssignmentProgresses.Where(p => p.CenterId == center && p.AssignmentId == assignmentId && !p.IsDeleted);
        if (assignment.Status != AssignmentStatus.Closed && await progress.AnyAsync(p =>
                p.StartedAt != null && p.Status != ProgressStatus.Completed, ct))
            return (409, "Còn bài đang làm. Hãy xử lý các lượt này hoặc đóng kỳ thi trước khi công bố.", null);
        var submitted = progress.Where(p => p.Status == ProgressStatus.Completed);
        if (!await submitted.AnyAsync(ct))
            return (409, "Chưa có bài thi đã nộp để công bố.", null);
        if (await submitted.AnyAsync(p => p.TeacherFinalReviewStatus != TeacherFinalReviewStatus.Approved, ct))
            return (409, "Hãy chốt kết quả của tất cả bài đã nộp trước khi công bố.", null);

        var before = JsonSerializer.Serialize(new { assignment.ResultsPublishedAt, assignment.Status });
        assignment.ResultsPublishedAt = now;
        assignment.Status = AssignmentStatus.Closed;
        assignment.RowVersion++;
        db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = center, ActorUserId = actor, TargetType = "Assignment", TargetId = assignmentId.ToString("D"),
            PermissionCode = "twin.reasoning.override", ActionType = "ExamResultsPublished",
            BeforeData = before, AfterData = JsonSerializer.Serialize(new { assignment.ResultsPublishedAt, assignment.Status }),
            Reason = "Giáo viên công bố đồng thời kết quả các bài thi đã nộp và đã chốt.",
            TraceId = traceId[..Math.Min(traceId.Length, 64)], CreatedAt = now, CreatedBy = actor
        });
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (409, "Bài thi vừa thay đổi. Hãy tải lại trước khi công bố.", null);
        }
        return (200, null, now);
    }
}
