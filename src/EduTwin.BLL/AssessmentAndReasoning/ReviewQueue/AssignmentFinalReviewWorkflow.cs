using System.Diagnostics;
using System.Text.Json;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public static class AssignmentFinalReviewWorkflow
{
    public const string LockedMessage = "Kết quả toàn bài đã chốt. Hãy mở lại để điều chỉnh và nhập lý do trước khi sửa đánh giá.";

    public static Task<bool> IsLockedAsync(EduTwinDbContext db, Guid centerId, Guid? assignmentId, Guid studentId, CancellationToken token) =>
        db.StudentAssignmentProgresses.AsNoTracking().AnyAsync(p => p.CenterId == centerId &&
            p.AssignmentId == assignmentId && p.StudentId == studentId && !p.IsDeleted &&
            p.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved, token);

    public static string Snapshot(StudentAssignmentProgress p) => JsonSerializer.Serialize(new
    {
        p.TeacherFinalReviewStatus, p.FinalReviewVersion, p.FinalReviewedAt, p.FinalReviewedByUserId, p.FinalTeacherNote
    });

    public static void Reopen(StudentAssignmentProgress p, Guid actorId, DateTime now)
    {
        p.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Pending;
        p.FinalReviewedAt = null;
        p.FinalReviewedByUserId = null;
        p.FinalTeacherNote = null;
        p.FinalReviewVersion++;
        p.IsOverallAiCommentStale = true;
        p.UpdatedAt = now;
        p.UpdatedBy = actorId;
    }

    public static void Audit(EduTwinDbContext db, StudentAssignmentProgress p, Guid actorId,
        string action, string before, string reason, DateTime now)
    {
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = p.CenterId, ActorUserId = actorId, TargetUserId = p.StudentId,
            ActionType = action, TargetType = "StudentAssignmentResult",
            TargetId = $"{p.AssignmentId:D}:{p.StudentId:D}", PermissionCode = "twin.reasoning.override",
            BeforeData = before, AfterData = Snapshot(p), Reason = reason,
            TraceId = traceId[..Math.Min(traceId.Length, 64)], CreatedAt = now, CreatedBy = actorId
        });
    }
}
