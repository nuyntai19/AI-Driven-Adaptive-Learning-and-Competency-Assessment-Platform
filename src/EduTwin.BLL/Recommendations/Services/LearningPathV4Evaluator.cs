using System.Text.Json;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Services;

/// <summary>Derives outcomes from issued submissions and the canonical evidence pipeline, never client scores.</summary>
public sealed class LearningPathV4Evaluator(EduTwinDbContext db, TimeProvider clock) : ILearningPathV4Evaluator
{
    public async Task<V4SessionEvaluationResultDto> EvaluateMissionRunAsync(Guid centerId, Guid studentId, ulong sessionRunId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await StudentLockHelper.AcquireStudentLockAsync(db, centerId, studentId, cancellationToken);
        var run = await db.LearningPathSessionRuns.Include(r => r.Session).FirstOrDefaultAsync(r => r.CenterId == centerId &&
            r.StudentId == studentId && r.SessionRunId == sessionRunId, cancellationToken) ?? throw new KeyNotFoundException("Lượt học không thuộc về bạn.");
        await LearningPathV4Data.SessionAsync(db, centerId, studentId, run.SessionId, cancellationToken);
        var activities = await db.ActivityRuns.Include(a => a.IssuedItems).ThenInclude(i => i.Attempt)
            .Where(a => a.CenterId == centerId && a.SessionRunId == sessionRunId).OrderBy(a => a.ActivityOrder).ToListAsync(cancellationToken);
        var items = activities.SelectMany(a => a.IssuedItems).ToList();
        var attempts = items.Where(i => i.Attempt != null).Select(i => i.Attempt!).ToList();
        var ids = attempts.Select(a => a.AttemptId).ToList();
        var evidence = await db.EvidenceAssessments.AsNoTracking().Where(e => e.CenterId == centerId && ids.Contains(e.AttemptId)).ToListAsync(cancellationToken);
        var latest = evidence.GroupBy(e => e.AttemptId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.EvaluatedAt).ThenByDescending(e => e.EvidenceAssessmentId).First());
        var analyses = await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == centerId && ids.Contains(a.AttemptId)).ToListAsync(cancellationToken);
        var pending = await db.AIAnalysisJobs.AnyAsync(j => j.CenterId == centerId && ids.Contains(j.AttemptId) &&
            (j.Status == AIJobStatus.Pending || j.Status == AIJobStatus.Processing), cancellationToken);
        var independent = activities.Where(a => a.ActivityType is "IndependentCheck" or "TransferChallenge").SelectMany(a => a.IssuedItems).ToList();
        var allDone = activities.Count > 0 && activities.All(a => a.Status is "Completed" or "Submitted");
        var result = new V4SessionEvaluationResultDto { SessionRunId = sessionRunId, SessionId = run.SessionId,
            TotalActivities = (uint)activities.Count, CompletedActivities = (uint)activities.Count(a => a.Status is "Completed" or "Submitted"), EvaluatedAt = now };
        string reason;
        if (!allDone)
        {
            result.Outcome = "InsufficientEvidence";
            reason = "Chưa hoàn thành đủ hoạt động của nhiệm vụ. Tiến độ đã được lưu để tiếp tục.";
        }
        else if (pending || attempts.Any(a => a.Status is AttemptStatus.PendingAnalysis or AttemptStatus.Processing))
        {
            result.Outcome = "PendingGrading";
            reason = "Bài làm đã lưu; đang chờ pipeline phân tích và kiểm tra bằng chứng.";
        }
        else if (attempts.Any(a => latest.TryGetValue(a.AttemptId, out var e) && (e.RequiresTeacherReview || e.TrustLevel == EvidenceTrustLevel.ReviewOnly)) ||
            analyses.Any(a => a.NeedsTeacherReview && a.ReviewDecision == null))
        {
            result.Outcome = "PendingTeacherReview"; result.HasPendingReview = true;
            reason = "Bằng chứng cần giáo viên xác nhận; chưa mở khóa phần phụ thuộc.";
        }
        else if (independent.Count == 0)
        {
            result.Outcome = "ActivityOnlyCompleted";
            reason = "Đã hoàn thành hoạt động học/luyện. Việc này không tự xác nhận đã nắm vững kỹ năng; hãy thực hiện kiểm tra độc lập khi đủ điều kiện.";
        }
        else
        {
            var valid = independent.Where(i => i.ExposureMode == "Independent" && i.SolutionExposedAt == null &&
                i.Attempt != null && !i.Attempt.IsPostFeedback && !i.Attempt.Skipped && i.Attempt.SolutionExposedAt == null &&
                latest.TryGetValue(i.Attempt.AttemptId, out var e) && !e.RequiresTeacherReview && e.TrustLevel != EvidenceTrustLevel.ReviewOnly)
                .GroupBy(i => i.QuestionId).Select(g => g.First()).ToList();
            if (valid.Count < 5 || valid.Count != independent.Count)
            {
                result.Outcome = "InsufficientEvidence";
                reason = $"Có {valid.Count}/5 câu độc lập đủ bằng chứng. Không kết luận đạt khi chưa có đánh giá hợp lệ.";
            }
            else
            {
                var correct = valid.Count(i => analyses.FirstOrDefault(a => a.AttemptId == i.AttemptId)?.OverrideIsCorrect ?? i.Attempt!.IsCorrect ?? false);
                result.Outcome = correct / (decimal)valid.Count >= .8m ? "Passed" : "NeedsSupport";
                reason = $"Kiểm tra độc lập: {correct}/{valid.Count} câu đúng; yêu cầu tối thiểu 80% với ít nhất 5 câu qua kiểm duyệt bằng chứng.";
                if (result.Outcome == "Passed") result.ProvenSkills.Add(run.Session.TopicNodeId.ToString());
                else result.SkillsNeedingSupport.Add(run.Session.TopicNodeId.ToString());
            }
        }
        result.DecisionReasons.Add(reason);
        var terminal = result.Outcome is "Passed" or "NeedsSupport" or "ActivityOnlyCompleted";
        var targetStatus = terminal ? SessionRunStatus.Completed : result.Outcome == "PendingGrading" ? SessionRunStatus.Grading :
            result.Outcome == "PendingTeacherReview" ? SessionRunStatus.NeedsReview : SessionRunStatus.InProgress;
        var outcome = result.Outcome == "Passed" ? SessionRunOutcome.Passed : result.Outcome == "NeedsSupport" ? SessionRunOutcome.NotPassed :
            result.Outcome == "InsufficientEvidence" ? SessionRunOutcome.InsufficientEvidence : SessionRunOutcome.None;
        var last = await db.PathDecisions.AsNoTracking().Where(d => d.CenterId == centerId && d.SessionRunId == sessionRunId &&
            d.TriggerType == "MissionEvaluation").OrderByDescending(d => d.DecisionId).FirstOrDefaultAsync(cancellationToken);
        var summary = $"{result.Outcome}: {reason}";
        if (last?.DecisionSummary != summary)
        {
            run.Status = targetStatus; run.Outcome = outcome; run.LastActivityAt = now; run.UpdatedAt = now; run.RowVersion++;
            run.CompletedAt = terminal ? now : null;
            run.EvaluationStatus = result.HasPendingReview ? SessionRunEvaluationStatus.AwaitingTeacher :
                result.Outcome == "PendingGrading" ? SessionRunEvaluationStatus.Processing : terminal ? SessionRunEvaluationStatus.Completed : SessionRunEvaluationStatus.NotStarted;
            run.Session.Status = result.Outcome is "Passed" or "ActivityOnlyCompleted" ? LearningPathSessionStatus.Completed :
                result.Outcome is "NeedsSupport" or "PendingTeacherReview" ? LearningPathSessionStatus.NeedsReview : LearningPathSessionStatus.InProgress;
            run.Session.UpdatedAt = now; run.Session.RowVersion++;
            db.PathDecisions.Add(new PathDecision { CenterId = centerId, LearningPathId = run.Session.LearningPathId, SessionRunId = sessionRunId,
                TriggerType = "MissionEvaluation", DecisionSummary = summary, PolicyVersion = LearningPathV4Data.Policy,
                ReasonCodes = JsonSerializer.SerializeToDocument(new[] { result.Outcome }), EvidenceReferenceIds = JsonSerializer.SerializeToDocument(latest.Values.Select(e => e.EvidenceAssessmentId)),
                CreatedAt = now, UpdatedAt = now, RowVersion = 1 });
            await db.SaveChangesAsync(cancellationToken);
        }
        await tx.CommitAsync(cancellationToken);
        // Next missions come from the real accepted route/agenda. No fabricated math mission and no direct Twin writes.
        return result;
    }

    public async Task<V4SessionEvaluationResultDto> EvaluateLateReviewAsync(Guid centerId, ulong attemptId, decimal overrideScore,
        CancellationToken cancellationToken = default)
    {
        var attempt = await db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.CenterId == centerId && a.AttemptId == attemptId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy bài làm.");
        if (!attempt.SessionRunId.HasValue) throw new InvalidOperationException("Bài làm không thuộc nhiệm vụ.");
        // TeacherApprove/TeacherOverride own grading. This adapter must never mutate append-only Attempt scores.
        return await EvaluateMissionRunAsync(centerId, attempt.StudentId, attempt.SessionRunId.Value, cancellationToken);
    }
}
