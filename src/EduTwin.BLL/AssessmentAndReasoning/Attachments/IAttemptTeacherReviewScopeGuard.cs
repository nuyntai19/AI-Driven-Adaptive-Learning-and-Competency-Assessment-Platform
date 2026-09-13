using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

/// <summary>
/// Unified scope guard protecting attempt review, attachment download, and teacher overrides.
/// Free-practice attempts (AssignmentId == null) are strictly Fail-Closed for teachers.
/// </summary>
public interface IAttemptTeacherReviewScopeGuard
{
    Task<bool> CanAccessAttemptAsync(
        Guid centerId,
        Guid actorUserId,
        string role,
        ulong attemptId,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessAttemptAsync(
        Guid centerId,
        Guid actorUserId,
        string role,
        Attempt attempt,
        CancellationToken cancellationToken = default);
}
