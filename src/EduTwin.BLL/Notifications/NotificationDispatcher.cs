using System.Data;
using System.Text.Json;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using EduTwin.BLL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

public sealed class NotificationDispatcher(EduTwinDbContext db, TimeProvider clock)
{
    public async Task<bool> DispatchAsync(Guid eventId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var state = await db.NotificationDispatches.SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (state is null || state.State is "Completed" or "DeadLetter" || state.AvailableAt > now ||
            state.State == "Processing" && state.LeaseUntil > now) return false;
        if (state.AttemptCount >= 5)
        {
            state.State = "DeadLetter";
            state.LastErrorCode = "RETRY_LIMIT";
            state.LeaseToken = null;
            state.LeaseUntil = null;
            return await SaveClaimAsync(ct);
        }
        var token = Guid.NewGuid();
        state.State = "Processing";
        state.LeaseToken = token;
        state.LeaseUntil = now.AddMinutes(2);
        state.AttemptCount++;
        if (!await SaveClaimAsync(ct)) return false;

        try
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
            var envelope = await db.NotificationEvents.AsNoTracking().SingleAsync(x => x.EventId == eventId, ct);
            var businessContext = NotificationBusinessContextSerializer.Read(envelope.ResourceContext);
            if (envelope.EventType is "AICompletionMarker" or "AssignmentAiReady" && businessContext?.StudentId is { } studentId)
            {
                await StudentLockHelper.AcquireStudentLockAsync(db, envelope.CenterId, studentId, ct);
                if (db.Database.IsRelational() && businessContext.AssignmentId is { } assignmentId)
                {
                    // Manual retry reuses the job row. Lock both attempt and job until the generation is checked and delivered.
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT attempt_id FROM attempts WHERE center_id={envelope.CenterId} AND assignment_id={assignmentId} AND student_id={studentId} ORDER BY attempt_id FOR UPDATE", ct);
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT j.analysis_job_id FROM ai_analysis_jobs j JOIN attempts a ON a.center_id=j.center_id AND a.attempt_id=j.attempt_id WHERE a.center_id={envelope.CenterId} AND a.assignment_id={assignmentId} AND a.student_id={studentId} ORDER BY j.analysis_job_id FOR UPDATE", ct);
                }
            }
            if (envelope.EventType is "AssignmentResultApproved" or "AssignmentResultReopened" &&
                businessContext?.StudentId is { } resultStudent && businessContext.AssignmentId is { } resultAssignment)
            {
                await StudentLockHelper.AcquireStudentLockAsync(db, envelope.CenterId, resultStudent, ct);
                if (db.Database.IsRelational())
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT progress_id FROM student_assignment_progress WHERE center_id={envelope.CenterId} AND assignment_id={resultAssignment} AND student_id={resultStudent} FOR UPDATE", ct);
            }
            if (envelope.EventType == "AICompletionMarker")
            {
                var ready = await NotificationAiCompletion.StageAggregateAsync(db, envelope, clock, ct);
                if (state.LeaseToken != token || state.LeaseUntil <= clock.GetUtcNow().UtcDateTime)
                    throw new DbUpdateConcurrencyException("Notification lease expired.");
                state.State = ready ? "Completed" : "Pending";
                if (!ready) { state.AttemptCount--; state.AvailableAt = clock.GetUtcNow().UtcDateTime.AddSeconds(15); }
                state.LeaseToken = null; state.LeaseUntil = null; state.LastErrorCode = null;
                await db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return ready;
            }
            var ids = JsonSerializer.Deserialize<Guid[]>(envelope.RecipientUserIds)
                ?? throw new InvalidOperationException("Invalid recipient snapshot.");
            if (ids.Length is < 1 or > 500 || ids.Distinct().Count() != ids.Length)
                throw new InvalidOperationException("Invalid recipient snapshot.");
            if (!await db.Centers.AnyAsync(c => c.CenterId == envelope.CenterId && !c.IsDeleted && c.Status == CenterStatus.Active, ct))
                throw new InvalidOperationException("Notification center is inactive.");
            var users = await db.Users.AsNoTracking().Where(NotificationRecipientScope.Matches(ids))
                .Where(x => x.Status == UserStatus.Active).Select(x => x.UserId).ToArrayAsync(ct);
            var active = users.ToHashSet();
            if (envelope.MessageId is not null)
                active.IntersectWith(await new NotificationAudiencePolicy(db).RevalidateAsync(envelope, ct));
            else if (envelope.ResourceContext is not null)
                active.IntersectWith(await NotificationBusinessScope.RevalidateAsync(db, envelope, ct));
            var delivered = await db.NotificationDeliveries.Where(x => x.EventId == eventId).Select(x => x.UserId).ToArrayAsync(ct);
            var existing = delivered.ToHashSet();
            foreach (var userId in ids.Where(active.Contains).Where(id => !existing.Contains(id)))
                db.NotificationDeliveries.Add(new() { CenterId = envelope.CenterId, EventId = eventId,
                    UserId = userId, DeliveredAt = clock.GetUtcNow().UtcDateTime });
            // This update is fenced by the RowVersion from the successful claim.
            // A competing expired-lease claim changes it, rolling back the entire fan-out.
            if (state.LeaseToken != token || state.LeaseUntil <= clock.GetUtcNow().UtcDateTime)
                throw new DbUpdateConcurrencyException("Notification lease expired.");
            state.State = "Completed";
            state.DeliveredCount = ids.Count(active.Contains);
            state.SuppressedCount = ids.Length - state.DeliveredCount;
            state.LeaseUntil = null;
            state.LeaseToken = null;
            state.LastErrorCode = null;
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
        catch
        {
            db.ChangeTracker.Clear();
            var current = await db.NotificationDispatches.SingleOrDefaultAsync(x => x.EventId == eventId, ct);
            if (current is null || current.LeaseToken != token) return false;
            current.State = current.AttemptCount >= 5 ? "DeadLetter" : "Pending";
            current.AvailableAt = clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(300, 5 * Math.Pow(2, current.AttemptCount)));
            current.LeaseUntil = null;
            current.LeaseToken = null;
            current.LastErrorCode = "DISPATCH_FAILED";
            await SaveClaimAsync(ct);
            return false;
        }
    }

    private async Task<bool> SaveClaimAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
    }
}
