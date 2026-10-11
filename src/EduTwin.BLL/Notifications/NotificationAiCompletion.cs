using System.Text.Json;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

internal static class NotificationAiCompletion
{
    // This check runs under the existing student lock at dispatch, not just when the aggregate is staged.
    public static async Task<string?> CurrentGenerationAsync(EduTwinDbContext db, Guid assignmentId, Guid studentId, CancellationToken ct)
    {
        var progress = await db.StudentAssignmentProgresses.AsNoTracking().SingleOrDefaultAsync(p => p.AssignmentId == assignmentId && p.StudentId == studentId, ct);
        if (progress?.Status != ProgressStatus.Completed || progress.CompletedAt == null) return null;
        var questions = await db.AssignmentQuestions.AsNoTracking().Where(q => q.AssignmentId == assignmentId && !q.IsVoided).Select(q => q.QuestionId).ToArrayAsync(ct);
        var attempts = (await db.Attempts.AsNoTracking().Where(a => a.AssignmentId == assignmentId && a.StudentId == studentId && questions.Contains(a.QuestionId))
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).ToArrayAsync(ct)).GroupBy(a => a.QuestionId).Select(g => g.First()).OrderBy(a => a.AttemptId).ToArray();
        if (attempts.Length == 0 || attempts.Length != questions.Length || attempts.Any(a => a.Status is AttemptStatus.PendingAnalysis or AttemptStatus.Processing)) return null;
        var ids = attempts.Select(a => a.AttemptId).ToArray();
        var jobs = (await db.AIAnalysisJobs.AsNoTracking().Where(j => ids.Contains(j.AttemptId)).OrderByDescending(j => j.AnalysisJobId).ToArrayAsync(ct))
            .GroupBy(j => j.AttemptId).ToDictionary(g => g.Key, g => g.First());
        if (jobs.Values.Any(j => j.Status is AIJobStatus.Pending or AIJobStatus.Processing) || attempts.Any(a => !a.Skipped && a.ManualRetryCount > 0 &&
            (!jobs.TryGetValue(a.AttemptId, out var j) || a.LastManualRetryAt is { } at && (j.CompletedAt ?? j.UpdatedAt) < at))) return null;
        return NotificationBusinessStager.Hash(string.Join(";", attempts.Select(a => $"{a.AttemptId}:{a.ManualRetryCount}:{jobs.GetValueOrDefault(a.AttemptId)?.AnalysisJobId ?? 0}")));
    }
    // Returns false while the effective submission is not fully terminal; a checkpoint alone is never sufficient.
    public static async Task<bool> StageAggregateAsync(EduTwinDbContext db, NotificationEvent marker, TimeProvider clock, CancellationToken ct)
    {
        var context = NotificationBusinessContextSerializer.Read(marker.ResourceContext);
        if (context?.AssignmentId is not { } assignmentId || context.StudentId is not { } studentId) return true;
        var assignment = await db.Assignments.AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == assignmentId, ct);
        var progress = await db.StudentAssignmentProgresses.AsNoTracking().SingleOrDefaultAsync(p => p.AssignmentId == assignmentId && p.StudentId == studentId, ct);
        if (assignment is null || progress is null) return true;
        if (progress.Status != ProgressStatus.Completed || progress.CompletedAt == null) return false;
        var questions = await db.AssignmentQuestions.AsNoTracking().Where(q => q.AssignmentId == assignmentId && !q.IsVoided).Select(q => q.QuestionId).ToArrayAsync(ct);
        var attempts = (await db.Attempts.AsNoTracking().Where(a => a.AssignmentId == assignmentId && a.StudentId == studentId && questions.Contains(a.QuestionId))
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).ToArrayAsync(ct)).GroupBy(a => a.QuestionId).Select(g => g.First()).OrderBy(a => a.AttemptId).ToArray();
        if (attempts.Length == 0) return true; // Voiding does not fabricate a submission or AI success.
        if (attempts.Length != questions.Length || attempts.Any(a => a.Status is AttemptStatus.PendingAnalysis or AttemptStatus.Processing)) return false;
        var ids = attempts.Select(a => a.AttemptId).ToArray();
        var jobs = (await db.AIAnalysisJobs.AsNoTracking().Where(j => ids.Contains(j.AttemptId)).OrderByDescending(j => j.AnalysisJobId).ToArrayAsync(ct))
            .GroupBy(j => j.AttemptId).ToDictionary(g => g.Key, g => g.First());
        if (jobs.Values.Any(j => j.Status is AIJobStatus.Pending or AIJobStatus.Processing)) return false;
        // A late result/marker of an older retry cannot announce completion of the current generation.
        if (attempts.Any(a => !a.Skipped && a.ManualRetryCount > 0 && (!jobs.TryGetValue(a.AttemptId, out var j) ||
            a.LastManualRetryAt is { } retryAt && (j.CompletedAt ?? j.UpdatedAt) < retryAt))) return false;
        var analyses = await db.ReasoningAnalyses.AsNoTracking().Where(a => ids.Contains(a.AttemptId)).Select(a => new { a.AttemptId, a.IsFallback, a.NeedsTeacherReview }).ToArrayAsync(ct);
        var success = jobs.Values.Count(j => j.Status == AIJobStatus.Completed && analyses.Any(a => a.AttemptId == j.AttemptId && !a.IsFallback));
        var fallback = jobs.Values.Count(j => j.Status == AIJobStatus.FallbackCompleted);
        var failed = jobs.Values.Count(j => j.Status == AIJobStatus.FailedTerminal);
        var missingAnalysis = jobs.Values.Count(j => j.Status == AIJobStatus.Completed && !analyses.Any(a => a.AttemptId == j.AttemptId));
        var skipped = attempts.Count(a => a.Skipped);
        var noAi = attempts.Count(a => !a.Skipped && !jobs.ContainsKey(a.AttemptId));
        var generation = NotificationBusinessStager.Hash(string.Join(";", attempts.Select(a => $"{a.AttemptId}:{a.ManualRetryCount}:{jobs.GetValueOrDefault(a.AttemptId)?.AnalysisJobId ?? 0}")));
        var @class = await db.Classes.AsNoTracking().SingleOrDefaultAsync(c => c.ClassId == assignment.ClassId, ct);
        if (@class is null) return true;
        var body = $"AI đã xử lý bài: {success} câu có phân tích AI, {fallback} câu fallback, {failed} câu lỗi terminal, {missingAnalysis} câu chưa có phân tích hợp lệ, {skipped} câu bỏ qua, {noAi} câu không có phân tích AI. " +
            "Đây chưa phải kết quả toàn bài đã chốt. Nhận xét/lộ trình tổng hợp có thể cập nhật sau; thông báo không khẳng định chúng đã sẵn sàng.";
        foreach (var (role, recipient, kind) in new[] { ("student", studentId, "HistoricalAssignment"), ("teacher", @class.TeacherId, "TeacherWork") })
        {
            var key = $"ai-ready:{assignmentId}:{studentId}:{generation}:{role}";
            // Hash the long aggregate key to the bounded, DB-unique idempotency column.
            key = "ai-ready:" + NotificationBusinessStager.Hash(key);
            if (await db.NotificationEvents.AnyAsync(e => e.DeduplicationKey == key, ct) || db.NotificationEvents.Local.Any(e => e.DeduplicationKey == key)) continue;
            var now = clock.GetUtcNow().UtcDateTime; var id = Guid.NewGuid();
            db.NotificationEvents.Add(new() { CenterId = marker.CenterId, EventId = id, CreatedAt = now, EventType = "AssignmentAiReady",
                DeduplicationKey = key, Title = "AI đã xử lý toàn bài", Body = body, RecipientUserIds = JsonSerializer.Serialize(new[] { recipient }),
                ResourceContext = JsonSerializer.Serialize(new NotificationBusinessContext(kind, @class.ClassId, assignmentId, studentId, Version: generation)) });
            db.NotificationDispatches.Add(new() { CenterId = marker.CenterId, EventId = id, AvailableAt = now, RowVersion = 1 });
        }
        // Retire sibling markers once the whole effective submission is terminal. Otherwise 100 markers
        // would sit ahead of the two user-facing events in a one-event-per-tenant fair worker.
        var assignmentToken = assignmentId.ToString("D");
        var siblingIds = db.NotificationEvents.Where(e => e.EventType == "AICompletionMarker" && e.RecipientUserIds == marker.RecipientUserIds &&
            e.ResourceContext != null && e.ResourceContext.Contains(assignmentToken)).Select(e => e.EventId);
        var siblings = await db.NotificationDispatches.Where(d => d.State == "Pending" && siblingIds.Contains(d.EventId)).Take(500).ToArrayAsync(ct);
        foreach (var sibling in siblings) { sibling.State = "Completed"; sibling.LeaseToken = null; sibling.LeaseUntil = null; sibling.LastErrorCode = null; }
        return true;
    }
}
