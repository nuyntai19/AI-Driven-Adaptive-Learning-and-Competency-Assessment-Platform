using System.Text.Json;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
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
        if (run.Status == SessionRunStatus.Abandoned)
            throw new InvalidOperationException("Lượt học này đã được hủy; các câu đã nộp vẫn được giữ trong lịch sử.");
        var activities = await db.ActivityRuns.Include(a => a.IssuedItems).ThenInclude(i => i.Attempt)
            .Where(a => a.CenterId == centerId && a.SessionRunId == sessionRunId).OrderBy(a => a.ActivityOrder).ToListAsync(cancellationToken);
        var items = activities.SelectMany(a => a.IssuedItems).ToList();
        var attempts = items.Where(i => i.Attempt != null).Select(i => i.Attempt!).ToList();
        var ids = attempts.Select(a => a.AttemptId).ToList();
        var evidence = ids.Count == 0 ? new List<EvidenceAssessment>() : await db.EvidenceAssessments.AsNoTracking().Where(e => e.CenterId == centerId && ids.Contains(e.AttemptId)).ToListAsync(cancellationToken);
        var latest = evidence.GroupBy(e => e.AttemptId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.EvaluatedAt).ThenByDescending(e => e.EvidenceAssessmentId).First());
        var analyses = ids.Count == 0 ? new List<ReasoningAnalysis>() : await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == centerId && ids.Contains(a.AttemptId)).ToListAsync(cancellationToken);
        var pending = ids.Count > 0 && await db.AIAnalysisJobs.AnyAsync(j => j.CenterId == centerId && ids.Contains(j.AttemptId) &&
            (j.Status == AIJobStatus.Pending || j.Status == AIJobStatus.Processing), cancellationToken);
        var independent = activities.Where(a => a.ActivityType is "IndependentCheck" or "TransferChallenge").SelectMany(a => a.IssuedItems).ToList();
        var baseline = run.PolicySnapshot?.RootElement.TryGetProperty("isPlacement", out var baselineFlag) == true && baselineFlag.ValueKind == JsonValueKind.True;
        if (baseline)
        {
            var questionIds = independent.Where(i => i.QuestionId.HasValue).Select(i => i.QuestionId!.Value).ToList();
            var targetIds = await db.Questions.Where(q => q.CenterId == centerId && questionIds.Contains(q.QuestionId) && q.PrimaryTopicNodeId == run.Session.TopicNodeId)
                .Select(q => q.QuestionId).ToListAsync(cancellationToken);
            independent = independent.Where(i => i.QuestionId.HasValue && targetIds.Contains(i.QuestionId.Value)).ToList();
        }
        var allDone = activities.Count > 0 && activities.All(a => a.Status is "Completed" or "Submitted");
        var result = new V4SessionEvaluationResultDto { SessionRunId = sessionRunId, SessionId = run.SessionId,
            TotalActivities = (uint)activities.Count, CompletedActivities = (uint)activities.Count(a => a.Status is "Completed" or "Submitted"), EvaluatedAt = now };
        string reason;
        if (!allDone && LearningAssessmentPolicy.ExpiresAt(run.PolicySnapshot, run.StartedAt) is { } expiry && now >= expiry)
        {
            result.Outcome = "TimedOut";
            reason = "Đã hết giờ. Giữ các câu đã nộp; phần chưa làm chưa đủ bằng chứng để kết luận. Lộ trình sẽ bổ sung rèn lại theo kết quả từng kỹ năng.";
        }
        else if (!allDone)
        {
            result.Outcome = "InsufficientEvidence";
            reason = "Chưa hoàn thành đủ hoạt động của nhiệm vụ. Tiến độ đã được lưu để tiếp tục.";
        }
        else if (pending || attempts.Any(a => a.Status is AttemptStatus.PendingAnalysis or AttemptStatus.Processing))
        {
            result.Outcome = "PendingGrading";
            reason = "Bài làm đã lưu; đang chờ pipeline phân tích và kiểm tra bằng chứng.";
        }
        // Learning path auto-approve: teacher review evidence is treated as provisionally valid.
        // Evidence metadata (RequiresTeacherReview, NeedsTeacherReview) is preserved for audit,
        // but the student's learning path is not blocked waiting for teacher action.
        else if (independent.Any(i => i.Attempt?.Skipped == true))
        {
            result.Outcome = "Inconclusive";
            reason = "Có câu đã bỏ qua nên phần này chưa đủ bằng chứng để kết luận đạt. Các câu đã nộp được giữ; tiếp tục học và kiểm tra bổ sung bằng câu khác.";
        }
        else if (independent.Count == 0)
        {
            result.Outcome = "ActivityOnlyCompleted";
            // Practice and review are scored (hinted answers count half) so the loop can step up, repeat or step down.
            var graded = activities.Where(a => a.ActivityType is "Practice" or "Review").SelectMany(a => a.IssuedItems.Select(i => (a, i)))
                .Where(x => x.i.Attempt != null && x.i.SolutionExposedAt == null && !x.i.Attempt.Skipped)
                .Select(x => (Correct: analyses.FirstOrDefault(r => r.AttemptId == x.i.AttemptId)?.OverrideIsCorrect ?? x.i.Attempt!.IsCorrect, Hinted: x.a.AssistanceRequestedCount > 0))
                .Where(x => x.Correct.HasValue).ToList();
            result.PracticeAccuracy = LearningPathPacing.Accuracy(graded.Count, graded.Count(x => x.Correct == true), graded.Count(x => x.Correct == true && x.Hinted));
            reason = result.PracticeAccuracy.HasValue
                ? $"Bộ luyện: đúng {graded.Count(x => x.Correct == true)}/{graded.Count} câu ({Math.Round(result.PracticeAccuracy.Value * 100)}%{(graded.Any(x => x.Correct == true && x.Hinted) ? ", câu có gợi ý tính một nửa" : "")}). Luyện tập không tự xác nhận đã nắm vững; bài tự kiểm tra mới xác nhận."
                : "Đã hoàn thành hoạt động học. Việc này không tự xác nhận đã nắm vững kỹ năng; hãy làm tự kiểm tra khi đủ điều kiện.";
        }
        else
        {
            var valid = independent.Where(i => i.ExposureMode == "Independent" && i.SolutionExposedAt == null &&
                i.Attempt != null && !i.Attempt.IsPostFeedback && !i.Attempt.Skipped && i.Attempt.SolutionExposedAt == null &&
                latest.TryGetValue(i.Attempt.AttemptId, out var _))
                .GroupBy(i => i.QuestionId).Select(g => g.First()).ToList();
            var shortProbe = run.PolicySnapshot?.RootElement.TryGetProperty("minimumIndependentQuestions", out var minimum) == true && minimum.GetInt32() == 3;
            if (valid.Count < (shortProbe ? 3 : 5) || valid.Count != independent.Count)
            {
                result.Outcome = "InsufficientEvidence";
                reason = $"Có {valid.Count}/5 câu độc lập đủ bằng chứng. Không kết luận đạt khi chưa có đánh giá hợp lệ.";
            }
            else
            {
                var correct = valid.Count(i => analyses.FirstOrDefault(a => a.AttemptId == i.AttemptId)?.OverrideIsCorrect ?? i.Attempt!.IsCorrect ?? false);
                var questionIds = valid.Select(i => i.QuestionId!.Value).ToList();
                var types = await db.Questions.AsNoTracking().Where(q => q.CenterId == centerId && questionIds.Contains(q.QuestionId))
                    .Select(q => new { q.QuestionId, q.PrimaryTopicNodeId, q.QuestionType, q.Difficulty }).ToListAsync(cancellationToken);
                // Multiple choice can be guessed: when most items are multiple choice, every answer must be correct.
                var guessable = types.Count(t => t.QuestionType == Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice) * 2 > valid.Count;
                var required = guessable && !baseline ? valid.Count : (int)Math.Ceiling(valid.Count * .8m);
                var classification = baseline ? LearningAssessmentPolicy.ClassifyPlacement(valid.Count, correct)
                    : LearningAssessmentPolicy.Classify(valid.Count, correct, guessable);
                // A run whose policy floor is 3 (small bank: the snapshot records the
                // effective minimum) may conclude on 3-4 valid answers - the agenda
                // already promises a low-confidence conclusion for such samples.
                // Without this, a skill whose bank holds fewer than 5 check-eligible
                // questions could never be confirmed and the route could never close.
                result.Outcome = baseline ? classification == "Strong" ? "Passed" : classification == "NeedsSupport" ? "NeedsSupport" : "Inconclusive"
                    : valid.Count < 5 && !shortProbe ? "Inconclusive" : correct >= required ? "Passed" : "NeedsSupport";
                if (!baseline && valid.Count < 5 && !shortProbe)
                {
                    var accumulated = await LearningPathV4Data.MapAsync(db, centerId, studentId, run.Session.LearningPath.SubjectId, run.Session.ClassId, now, cancellationToken);
                    if (accumulated.Skills.Any(s => s.SkillId == run.Session.TopicNodeId.ToString() && s.State == "Confirmed")) result.Outcome = "Passed";
                }
                reason = $"Tự kiểm tra: đúng {correct}/{valid.Count} câu, cần {required}/{valid.Count}{(guessable && !baseline ? " vì phần lớn là trắc nghiệm" : "")}.";
                if (!baseline && shortProbe && valid.Count < 5) reason += $" Mẫu nhỏ ({valid.Count} câu) nên kết luận có độ tin cậy thấp.";
                if (!types.Any(t => t.Difficulty >= 3)) reason += " Bộ này chưa có câu mức vừa/khó nên chỉ xác nhận mức cơ bản.";
                // Auto-approved AI evidence keeps the route moving, but the result stays provisional until a teacher confirms it.
                result.IsProvisional = result.Outcome == "Passed" && valid.Any(i => latest[i.Attempt!.AttemptId].RequiresTeacherReview);
                if (result.IsProvisional) reason += " Tạm đạt: còn câu chờ giáo viên xác nhận, kết quả có thể thay đổi.";
                var isExam = run.PolicySnapshot?.RootElement.TryGetProperty("isMilestoneExam", out var examFlag) == true && examFlag.ValueKind == JsonValueKind.True;
                if (isExam)
                {
                    foreach (var skillGroup in types.GroupBy(q => q.PrimaryTopicNodeId))
                    {
                        var skillQuestions = skillGroup.Select(q => q.QuestionId).ToHashSet();
                        var skillAnswers = valid.Where(i => i.QuestionId.HasValue && skillQuestions.Contains(i.QuestionId.Value)).ToList();
                        var skillCorrect = skillAnswers.Count(i => analyses.FirstOrDefault(a => a.AttemptId == i.AttemptId)?.OverrideIsCorrect ?? i.Attempt!.IsCorrect ?? false);
                        var state = LearningAssessmentPolicy.Classify(skillAnswers.Count, skillCorrect, skillGroup.Count(q => q.QuestionType == Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice) * 2 > skillAnswers.Count);
                        if (state == "Strong") result.ProvenSkills.Add(skillGroup.Key.ToString()); else result.SkillsNeedingSupport.Add(skillGroup.Key.ToString());
                    }
                    var definition = JsonSerializer.Deserialize<PathProposalDto>(run.Session.LearningPath.PlanJson!.RootElement.GetRawText(), LearningPathV4Data.Json);
                    // Never pass an exam milestone below 80%; a higher target score raises the bar.
                    var reference = Math.Max(.8m, (definition?.TargetScore ?? 8m) / 10m);
                    result.Outcome = correct / (decimal)valid.Count >= reference ? "Passed" : "NeedsSupport";
                    reason = $"Mốc thi: đúng {correct}/{valid.Count} câu; đối chiếu điểm mục tiêu {definition?.TargetScore ?? 8m}/10. Mức vững được kết luận riêng theo từng kỹ năng, không lấy điểm tổng để xác nhận tất cả.";
                }
                else if (result.Outcome == "Passed") result.ProvenSkills.Add(run.Session.TopicNodeId.ToString());
                else result.SkillsNeedingSupport.Add(run.Session.TopicNodeId.ToString());
            }
        }
        result.DecisionReasons.Add(reason);
        var terminal = result.Outcome is "Passed" or "NeedsSupport" or "Inconclusive" or "TimedOut" or "ActivityOnlyCompleted";
        var targetStatus = terminal ? SessionRunStatus.Completed : result.Outcome == "PendingGrading" ? SessionRunStatus.Grading :
            result.Outcome == "PendingTeacherReview" ? SessionRunStatus.NeedsReview : SessionRunStatus.InProgress;
        var outcome = result.Outcome == "Passed" ? SessionRunOutcome.Passed : result.Outcome is "NeedsSupport" or "TimedOut" ? SessionRunOutcome.NotPassed :
            result.Outcome is "InsufficientEvidence" or "Inconclusive" ? SessionRunOutcome.InsufficientEvidence : SessionRunOutcome.None;
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
                result.Outcome is "NeedsSupport" or "PendingTeacherReview" or "TimedOut" or "Inconclusive" ? LearningPathSessionStatus.NeedsReview : LearningPathSessionStatus.InProgress;
            if (terminal && run.Session.LearningPath.PlanJson?.RootElement.TryGetProperty("isPlacement", out var placement) == true && placement.ValueKind == JsonValueKind.True)
                run.Session.Status = LearningPathSessionStatus.Completed;
            run.Session.UpdatedAt = now; run.Session.RowVersion++;
            db.PathDecisions.Add(new PathDecision { CenterId = centerId, LearningPathId = run.Session.LearningPathId, SessionRunId = sessionRunId,
                TriggerType = "MissionEvaluation", DecisionSummary = summary, PolicyVersion = LearningPathV4Data.Policy,
                ReasonCodes = JsonSerializer.SerializeToDocument(new[] { result.Outcome }), EvidenceReferenceIds = JsonSerializer.SerializeToDocument(latest.Values.Select(e => e.EvidenceAssessmentId)),
                CreatedAt = now, UpdatedAt = now, RowVersion = 1 });
            await LearningPathFollowUp.AddAsync(db, run, result.Outcome, now, cancellationToken, result.PracticeAccuracy);
            if (terminal) await new DigitalTwin.StudentGoalRiskUpdater(db).UpdateAsync(centerId, studentId, run.Session.LearningPath.SubjectId, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
        var followUp = $"FollowUp:{sessionRunId}:";
        result.NextStep = await db.PathDecisions.AsNoTracking().Where(d => d.CenterId == centerId && (d.TriggerType.StartsWith(followUp) || d.TriggerType == $"ExamFollowUp:{sessionRunId}"))
            .OrderByDescending(d => d.DecisionId).Select(d => d.DecisionSummary).FirstOrDefaultAsync(cancellationToken);
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
