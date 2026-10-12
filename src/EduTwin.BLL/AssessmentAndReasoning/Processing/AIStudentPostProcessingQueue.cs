using EduTwin.BLL.Assignments;
using EduTwin.BLL.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public interface IAIStudentPostProcessingQueue
{
    Task EnqueueAsync(Guid center, Guid student, Guid subject, Guid? assignment, ulong attempt, DateTime now, CancellationToken token);
}

public sealed class AIStudentPostProcessingQueue(EduTwinDbContext db) : IAIStudentPostProcessingQueue
{
    // Called under the grading transaction/student lock; SaveChanges is owned by the caller.
    public async Task EnqueueAsync(Guid center, Guid student, Guid subject, Guid? assignment, ulong attempt, DateTime now, CancellationToken token)
    {
        var scope = assignment ?? Guid.Empty;
        var job = await db.AIStudentPostProcessingJobs.SingleOrDefaultAsync(x => x.CenterId == center &&
            x.StudentId == student && x.SubjectId == subject && x.AssignmentScopeId == scope, token);
        if (job is null)
        {
            job = new AIStudentPostProcessingJob { CenterId = center, StudentId = student, SubjectId = subject,
                AssignmentScopeId = scope, AvailableAt = now.AddSeconds(2) };
            db.AIStudentPostProcessingJobs.Add(job);
        }
        else if (job.Revision == job.ProcessedRevision) job.AvailableAt = now.AddSeconds(2);
        // Do not push AvailableAt forward indefinitely during a busy exam.
        job.Revision++;
        job.SourceAttemptId = attempt;
        job.TriggerAt = now;
        job.UpdatedAt = now;
    }
}

public sealed class AIStudentPostProcessor(
    EduTwinDbContext db, IRecommendationEngine recommendations,
    IOverallAssignmentCommentWorkflow comments, TimeProvider clock)
{
    public async Task<bool> RunOneAsync(Guid student, Guid subject, Guid assignmentScope, string owner, CancellationToken token)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var center = db.CurrentTenantId;
        AIStudentPostProcessingJob? claimed;
        ulong revision;
        await using (var transaction = await db.Database.BeginTransactionAsync(token))
        {
            await StudentLockHelper.AcquireStudentLockAsync(db, center, student, token);
            claimed = await db.AIStudentPostProcessingJobs.SingleOrDefaultAsync(x => x.CenterId == center &&
                x.StudentId == student && x.SubjectId == subject && x.AssignmentScopeId == assignmentScope, token);
            var now = clock.GetUtcNow().UtcDateTime;
            // Match datetime(6) precision so the persisted lease can be fenced by exact equality.
            now = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
            if (claimed is null || claimed.Revision <= claimed.ProcessedRevision || claimed.AvailableAt > now || claimed.LeaseUntil > now) return false;
            revision = claimed.Revision;
            claimed.LeaseOwner = owner;
            claimed.LeaseUntil = now.AddMinutes(2);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        db.ChangeTracker.Clear();
        var succeeded = false;
        var waitingForAnalyses = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            if (assignmentScope != Guid.Empty)
            {
                var hasProcessing = await db.Attempts.AsNoTracking().AnyAsync(x => x.CenterId == center &&
                    x.StudentId == student && x.AssignmentId == assignmentScope &&
                    (x.Status == EduTwin.Contracts.AssessmentAndReasoning.AttemptStatus.PendingAnalysis ||
                     x.Status == EduTwin.Contracts.AssessmentAndReasoning.AttemptStatus.Processing), timeout.Token);
                // Keep this revision pending until synthesis can include all completed analyses.
                if (hasProcessing) throw new AIAnalysisPostProcessingPendingException();
            }
            await recommendations.GenerateAndPersistAsync(center, student, subject, claimed.SourceAttemptId, claimed.TriggerAt, timeout.Token);
            if (assignmentScope != Guid.Empty)
                await comments.GenerateAndCacheOverallCommentAsync(center, assignmentScope, student, timeout.Token);
            succeeded = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (AIAnalysisPostProcessingPendingException) { waitingForAnalyses = true; }
        catch (Exception) { /* Durable retry below; no submission/provider contents in logs. */ }
        finally
        {
            db.ChangeTracker.Clear();
            // Cancellation leaves the durable lease to expire, rather than claiming successful processing.
            if (!token.IsCancellationRequested)
            {
                await using var transaction = await db.Database.BeginTransactionAsync(token);
                await StudentLockHelper.AcquireStudentLockAsync(db, center, student, token);
                var current = await db.AIStudentPostProcessingJobs.SingleAsync(x => x.CenterId == center &&
                    x.StudentId == student && x.SubjectId == subject && x.AssignmentScopeId == assignmentScope, token);
                if (current.LeaseOwner == owner && current.LeaseUntil == claimed.LeaseUntil)
                {
                    current.ProcessedRevision = succeeded ? Math.Max(current.ProcessedRevision, revision) : current.ProcessedRevision;
                    current.FailureCount = succeeded ? 0 : waitingForAnalyses ? current.FailureCount : Math.Min(current.FailureCount + 1, 20);
                    current.LastErrorCode = succeeded ? null : waitingForAnalyses ? "AI_WAITING_FOR_ANALYSES" : "AI_POST_PROCESSING_RETRY";
                    current.AvailableAt = clock.GetUtcNow().UtcDateTime.AddSeconds(succeeded || waitingForAnalyses ? 2 : Math.Min(60, Math.Pow(2, current.FailureCount)));
                    current.LeaseOwner = null;
                    current.LeaseUntil = null;
                    await db.SaveChangesAsync(token);
                    await transaction.CommitAsync(token);
                }
            }
        }
        AIProcessingMetrics.Outcomes.Add(1, new KeyValuePair<string, object?>("stage", "post_processing"), new("outcome", succeeded ? "completed" : "retry"));
        AIProcessingMetrics.Duration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            new KeyValuePair<string, object?>("stage", "post_processing"));
        return succeeded;
    }

    private sealed class AIAnalysisPostProcessingPendingException : Exception;
}
