using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Services;

public sealed class LearningActivityExecutionService(EduTwinDbContext db, TimeProvider clock,
    ISubmitAttemptUseCase? submitAttempt = null, ILearningSessionExecutionService? questions = null) : ILearningActivityExecutionService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<MissionRunDto> StartMissionAsync(Guid centerId, Guid studentId, Guid missionId, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await StudentLockHelper.AcquireStudentLockAsync(db, centerId, studentId, cancellationToken);
        var session = await LearningPathV4Data.SessionAsync(db, centerId, studentId, missionId, cancellationToken);
        var run = await db.LearningPathSessionRuns.Include(r => r.Session).Where(r => r.CenterId == centerId &&
            r.StudentId == studentId && r.SessionId == missionId).OrderByDescending(r => r.RunNumber).FirstOrDefaultAsync(cancellationToken);
        if (run != null && (run.Status != SessionRunStatus.Completed || session.Status == LearningPathSessionStatus.Completed))
        {
            await tx.CommitAsync(cancellationToken);
            return await RunDtoAsync(run, cancellationToken);
        }
        if (session.LearningPath.Status != LearningPathStatus.Active) throw new InvalidOperationException("Tuyến này đã được thay thế; hãy chọn nhiệm vụ trong tuyến hiện tại.");
        if (await db.LearningPathSessionRuns.AnyAsync(r => r.CenterId == centerId && r.StudentId == studentId &&
            r.Session.LearningPath.SubjectId == session.LearningPath.SubjectId && r.SessionId != missionId && r.Status == SessionRunStatus.InProgress, cancellationToken))
            throw new InvalidOperationException("Bạn đang có nhiệm vụ chưa hoàn tất trong môn này. Hãy tiếp tục nhiệm vụ đó trước.");
        var node = (await LearningPathV4Data.NodesAsync(db, centerId, studentId, session.LearningPath.SubjectId, session.ClassId, cancellationToken))
            .FirstOrDefault(n => n.NodeId == session.TopicNodeId) ?? throw new InvalidOperationException("Kỹ năng không còn khả dụng.");
        var plan = JsonSerializer.Deserialize<PathProposalDto>(session.LearningPath.PlanJson!.RootElement.GetRawText(), LearningPathV4Data.Json);
        var definition = plan?.Milestones.SelectMany(m => m.Missions).FirstOrDefault(m => m.MissionId == missionId.ToString())
            ?? throw new InvalidOperationException("Nhiệm vụ không có trong bản kế hoạch đã xác nhận.");
        if (!definition.CanStart) throw new InvalidOperationException(definition.SelectionReason);
        var pool = await LearningPathV4Data.QuestionsAsync(db, centerId, session.LearningPath.SubjectId, new[] { node.NodeId }, cancellationToken);
        var independent = definition.Purpose == "IndependentCheck";
        if (independent)
        {
            var map = await LearningPathV4Data.MapAsync(db, centerId, studentId, session.LearningPath.SubjectId, session.ClassId, Now, cancellationToken);
            if (map.Skills.Single(s => s.SkillId == node.NodeId.ToString()).Availability == "PrerequisiteBlocked")
                throw new InvalidOperationException("Cần kiểm tra các kỹ năng nền tảng trước khi xác nhận kỹ năng này.");
            // Any question already answered or shown as a worked example is unsuitable for a new independent check.
            var answered = await db.Attempts.Where(a => a.CenterId == centerId && a.StudentId == studentId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken);
            var exposed = await db.IssuedItems.Where(i => i.CenterId == centerId && i.ActivityRun.SessionRun.StudentId == studentId &&
                i.SolutionExposedAt != null && i.QuestionId != null).Select(i => i.QuestionId!.Value).Distinct().ToListAsync(cancellationToken);
            pool = pool.Where(q => !answered.Contains(q.QuestionId) && !exposed.Contains(q.QuestionId)).ToList();
            if (pool.Count < 5) throw new InvalidOperationException($"Còn {pool.Count}/5 câu mới đủ điều kiện. Cần giáo viên bổ sung câu kiểm tra; không dùng lại câu đã lộ đáp án.");
        }
        var number = (run?.RunNumber ?? 0) + 1;
        run = new LearningPathSessionRun
        {
            CenterId = centerId, StudentId = studentId, SessionId = missionId, Session = session, RunNumber = number,
            Status = SessionRunStatus.InProgress, Outcome = SessionRunOutcome.None, StartedAt = Now, LastActivityAt = Now,
            CreatedAt = Now, UpdatedAt = Now, RowVersion = 1,
            PolicySnapshot = JsonSerializer.SerializeToDocument(new { minimumIndependentQuestions = 5, requiredCorrectRate = .8m, type = definition.Purpose, version = LearningPathV4Data.Policy })
        };
        db.LearningPathSessionRuns.Add(run);
        var activities = new List<ActivityRun>();
        void Add(string type, Guid? contentId = null, ulong? questionId = null)
        {
            var a = new ActivityRun { CenterId = centerId, SessionRun = run, ActivityOrder = (uint)activities.Count + 1,
                ActivityType = type, ContentVersionId = contentId, Status = "InProgress", StartedAt = Now,
                CreatedAt = Now, UpdatedAt = Now, RowVersion = 1, DraftRevision = 1 };
            if (questionId.HasValue) a.IssuedItems.Add(new IssuedItem { CenterId = centerId, QuestionId = questionId, ItemOrder = 1,
                ExposureMode = independent ? "Independent" : type == "Study" ? "SolutionExposed" : "Guided",
                SolutionExposedAt = type == "Study" ? Now : null, IssuedAt = Now, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
            activities.Add(a); db.ActivityRuns.Add(a);
        }
        if (definition.ContentVersionId.HasValue)
        {
            var content = await db.LearningContentVersions.SingleOrDefaultAsync(c => c.CenterId == centerId &&
                c.ContentVersionId == definition.ContentVersionId && c.SubjectId == session.LearningPath.SubjectId && c.Status == "Published", cancellationToken);
            if (content == null || !LearningPathV4Data.Matches(content, node)) throw new InvalidOperationException("Nội dung đã thu hồi hoặc không thuộc kỹ năng này.");
            Add(content.ActivityType, content.ContentVersionId);
        }
        else if (definition.Purpose == "Study")
        {
            var used = await db.Attempts.Where(a => a.CenterId == centerId && a.StudentId == studentId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken);
            // Reserve five unseen questions for confirmation instead of exposing their solutions during the lesson.
            var example = pool.FirstOrDefault(q => used.Contains(q.QuestionId) && !string.IsNullOrWhiteSpace(q.Solution))
                ?? (pool.Count > 5 ? pool.FirstOrDefault(q => !string.IsNullOrWhiteSpace(q.Solution)) : null);
            Add("Study", questionId: example?.QuestionId);
        }
        else
        {
            if (!independent)
            {
                var used = await db.Attempts.Where(a => a.CenterId == centerId && a.StudentId == studentId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken);
                var reusable = pool.Where(q => used.Contains(q.QuestionId)).ToList();
                var fresh = pool.Where(q => !used.Contains(q.QuestionId)).ToList();
                pool = reusable.Concat(fresh.Take(Math.Max(0, fresh.Count - 5))).ToList();
            }
            if (pool.Count == 0) throw new InvalidOperationException("Chưa có câu hỏi hợp lệ cho nhiệm vụ này.");
            foreach (var q in pool.Take(independent ? 5 : 3)) Add(definition.Purpose, questionId: q.QuestionId);
        }
        var assigned = activities.SelectMany(a => a.IssuedItems).Where(i => i.QuestionId.HasValue && i.SolutionExposedAt == null).Select(i => i.QuestionId!.Value).ToList();
        run.QuestionsAssigned = JsonSerializer.SerializeToDocument(assigned);
        run.TotalQuestions = (uint)assigned.Count;
        run.CurrentQuestionId = assigned.Count > 0 ? assigned[0] : null;
        session.Status = LearningPathSessionStatus.InProgress; session.TotalRuns = number; session.RowVersion++; session.UpdatedAt = Now;
        await db.SaveChangesAsync(cancellationToken);
        session.CurrentRunId = run.SessionRunId; await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await RunDtoAsync(run, cancellationToken);
    }

    private async Task<ActivityRun> LoadAsync(Guid center, Guid student, ulong id, CancellationToken ct)
    {
        var a = await db.ActivityRuns.Include(a => a.SessionRun).ThenInclude(r => r.Session).ThenInclude(s => s.LearningPath)
            .Include(a => a.ContentVersion).Include(a => a.IssuedItems).ThenInclude(i => i.Attempt)
            .FirstOrDefaultAsync(a => a.CenterId == center && a.ActivityRunId == id && a.SessionRun.StudentId == student, ct)
            ?? throw new KeyNotFoundException("Hoạt động không thuộc nhiệm vụ của bạn.");
        await LearningPathV4Data.SessionAsync(db, center, student, a.SessionRun.SessionId, ct);
        if (a.ContentVersion != null && a.ContentVersion.Status != "Published") throw new InvalidOperationException("Nội dung hoạt động đã thu hồi.");
        return a;
    }

    private async Task EnsureEditableAsync(ActivityRun activity, CancellationToken ct)
    {
        if (activity.SessionRun.Status != SessionRunStatus.InProgress || activity.Status == "Completed")
            throw new InvalidOperationException("Hoạt động đã nộp hoặc đang chờ đánh giá; không thể sửa.");
        if (await db.ActivityRuns.AnyAsync(a => a.CenterId == activity.CenterId && a.SessionRunId == activity.SessionRunId &&
            a.ActivityOrder < activity.ActivityOrder && a.Status != "Completed" && a.Status != "Submitted", ct))
            throw new InvalidOperationException("Hãy hoàn thành hoạt động trước đó.");
    }

    public async Task<ActivityRunDto> GetActivityRunAsync(Guid centerId, Guid studentId, ulong activityRunId, CancellationToken cancellationToken = default)
        => await DtoAsync(await LoadAsync(centerId, studentId, activityRunId, cancellationToken), cancellationToken);

    public async Task<SaveActivityDraftResponse> SaveDraftAsync(Guid centerId, Guid studentId, ulong activityRunId, SaveActivityDraftRequest request, CancellationToken cancellationToken = default)
    {
        var activity = await LoadAsync(centerId, studentId, activityRunId, cancellationToken);
        await EnsureEditableAsync(activity, cancellationToken);
        if (request.DraftPayload.Length > 100000) throw new ArgumentException("Bản nháp vượt giới hạn.");
        if (request.ExpectedRevision != activity.DraftRevision)
            return new SaveActivityDraftResponse { ActivityRunId = activityRunId, DraftRevision = activity.DraftRevision, HasConflict = true, SavedAt = activity.UpdatedAt };
        activity.DraftPayload = request.DraftPayload; activity.DraftRevision++; activity.RowVersion++; activity.UpdatedAt = Now;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(activity).ReloadAsync(cancellationToken);
            return new SaveActivityDraftResponse { ActivityRunId = activityRunId, DraftRevision = activity.DraftRevision, HasConflict = true, SavedAt = activity.UpdatedAt };
        }
        return new SaveActivityDraftResponse { ActivityRunId = activityRunId, DraftRevision = activity.DraftRevision, SavedAt = Now };
    }

    public async Task<AssistanceResponseDto> RequestAssistanceAsync(Guid centerId, Guid studentId, ulong activityRunId, RequestAssistanceRequest request, CancellationToken cancellationToken = default)
    {
        var a = await LoadAsync(centerId, studentId, activityRunId, cancellationToken);
        await EnsureEditableAsync(a, cancellationToken);
        var blocked = a.ActivityType is "IndependentCheck" or "TransferChallenge";
        a.AssistanceRequestedCount++; a.RowVersion++; a.UpdatedAt = Now;
        await db.SaveChangesAsync(cancellationToken);
        return new AssistanceResponseDto { IsBlocked = blocked, AssistanceMode = blocked ? "Blocked" : "HintOnly", FallbackUsed = true,
            Explanation = blocked ? "Phần tự kiểm tra không cung cấp gợi ý hoặc lời giải trước khi nộp."
                : a.ContentVersion?.Description ?? a.SessionRun.Session.Rationale ?? "Hãy xem lại phần kiến thức của kỹ năng này và xác định dữ kiện cần dùng.",
            PolicyNotice = blocked ? "Trợ giúp bị chặn ở phía server trong kiểm tra độc lập." : "Hướng dẫn theo nội dung đã có; không suy đoán lời giải khi thiếu dữ liệu." };
    }

    public async Task<ActivitySubmissionResponseDto> SubmitActivityAsync(Guid centerId, Guid studentId, ulong activityRunId, SubmitActivityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ClientSubmissionId == Guid.Empty || request.FinalAnswer.Length > 20000 || request.ReasoningText?.Length > 50000)
            throw new ArgumentException("Thiếu mã lần nộp hoặc nội dung vượt giới hạn.");
        var a = await LoadAsync(centerId, studentId, activityRunId, cancellationToken);
        var issued = a.IssuedItems.SingleOrDefault();
        if (issued?.Attempt != null)
        {
            if (issued.Attempt.ClientSubmissionId != request.ClientSubmissionId || request.QuestionId != issued.QuestionId ||
                issued.Attempt.FinalAnswer != request.FinalAnswer || issued.Attempt.ReasoningText != request.ReasoningText)
                throw new InvalidOperationException("Hoạt động đã nộp với nội dung khác.");
            return Response(a, issued.AttemptId, issued.Attempt.IsCorrect, "Bài làm đã được lưu; kết quả chính thức theo pipeline đánh giá.");
        }
        if (a.Status == "Completed")
        {
            var previous = JsonSerializer.Deserialize<SubmitActivityRequest>(a.DraftPayload ?? "{}", LearningPathV4Data.Json);
            if (previous?.ClientSubmissionId != request.ClientSubmissionId || previous.FinalAnswer != request.FinalAnswer || previous.SelectedErrorStep != request.SelectedErrorStep)
                throw new InvalidOperationException("Hoạt động đã nộp với nội dung khác.");
            return Response(a, null, a.IsMastered, a.Feedback ?? "Đã lưu.");
        }
        await EnsureEditableAsync(a, cancellationToken);
        if (a.ActivityType is "Practice" or "IndependentCheck" or "TransferChallenge")
        {
            if (issued?.QuestionId == null || request.QuestionId != issued.QuestionId) throw new ArgumentException("Câu hỏi không khớp câu đã phát.");
            if (submitAttempt == null) throw new InvalidOperationException("Chưa cấu hình pipeline nộp bài.");
            var result = await submitAttempt.ExecuteAsync(new SubmitAttemptRequest
            {
                ClientSubmissionId = request.ClientSubmissionId, SessionRunId = a.SessionRunId, QuestionId = issued.QuestionId.Value.ToString(),
                ClassId = a.SessionRun.Session.ClassId, FinalAnswer = request.FinalAnswer, ReasoningText = request.ReasoningText,
                AnswerDisplayLatex = request.AnswerDisplayLatex, DrawingUploadToken = request.DrawingUploadToken,
                TimeSpentSeconds = (uint)Math.Clamp(request.TimeSpentSeconds, 0, 86400)
            }, $"mission:{a.SessionRunId}", cancellationToken);
            if (!result.IsSuccess) throw new InvalidOperationException($"Không thể lưu bài làm: {result.ErrorCode}.");
            // The canonical submit transaction links IssuedItem + ActivityRun before committing.
            return Response(a, ulong.Parse(result.Data!.AttemptId), null, "Bài làm đã lưu và được chuyển tới pipeline chấm/kiểm duyệt hiện có.");
        }
        bool? correct = null;
        if (a.ActivityType == "FindError")
        {
            var rubric = a.ContentVersion?.Rubric?.RootElement;
            if (rubric == null || !rubric.Value.TryGetProperty("errorStep", out var step) || !rubric.Value.TryGetProperty("expectedAnswer", out var answer))
                throw new InvalidOperationException("Nội dung tìm lỗi chưa có barem hợp lệ; cần giáo viên bổ sung.");
            correct = request.SelectedErrorStep == step.ToString() && request.FinalAnswer.Trim() == answer.GetString()?.Trim();
        }
        else if (string.IsNullOrWhiteSpace(request.FinalAnswer)) throw new ArgumentException("Hãy trả lời câu hỏi nhìn lại trước khi hoàn thành hoạt động.");
        a.Status = "Completed"; a.IsMastered = null; a.Score = null; a.MaxScore = null;
        a.Feedback = correct.HasValue ? (correct.Value ? "Đã xác định đúng lỗi theo barem nội dung." : "Chưa khớp barem; hãy xem lại nội dung và hỏi giáo viên nếu cần.")
            : "Đã hoàn thành hoạt động học. Kết quả này không tự xác nhận năng lực; cần kiểm tra độc lập.";
        a.DraftPayload = JsonSerializer.Serialize(request, LearningPathV4Data.Json); a.DraftRevision++; a.RowVersion++; a.CompletedAt = Now; a.UpdatedAt = Now;
        await db.SaveChangesAsync(cancellationToken);
        return Response(a, null, correct, a.Feedback);
    }

    private static ActivitySubmissionResponseDto Response(ActivityRun a, ulong? attempt, bool? correct, string feedback) => new()
    { ActivityRunId = a.ActivityRunId, AttemptId = attempt, IsCorrect = correct, ActivityStatus = a.Status,
        OutcomeFeedback = feedback, NextActivityOrder = a.ActivityOrder + 1 };

    private async Task<MissionRunDto> RunDtoAsync(LearningPathSessionRun run, CancellationToken ct)
    {
        var ids = await db.ActivityRuns.Where(a => a.CenterId == run.CenterId && a.SessionRunId == run.SessionRunId)
            .OrderBy(a => a.ActivityOrder).Select(a => a.ActivityRunId).ToListAsync(ct);
        var result = new MissionRunDto { SessionRunId = run.SessionRunId, SessionId = run.SessionId, Title = run.Session.Title, Status = run.Status.ToString() };
        foreach (var id in ids) result.Activities.Add(await GetActivityRunAsync(run.CenterId, run.StudentId, id, ct));
        return result;
    }

    private async Task<ActivityRunDto> DtoAsync(ActivityRun a, CancellationToken ct)
    {
        var result = new ActivityRunDto { ActivityRunId = a.ActivityRunId, SessionRunId = a.SessionRunId, ActivityOrder = a.ActivityOrder,
            ActivityType = a.ActivityType, Status = a.Status, DraftPayload = a.DraftPayload, DraftRevision = a.DraftRevision,
            AssistanceRequestedCount = a.AssistanceRequestedCount, AssistanceMode = a.AssistanceMode, Feedback = a.Feedback,
            StartedAt = a.StartedAt, CompletedAt = a.CompletedAt };
        if (a.ContentVersion != null)
        {
            // Never send the grading rubric to the student. Published learning activities may contain worked steps.
            result.Content = new ActivityContentDto { ContentVersionId = a.ContentVersion.ContentVersionId, SkillId = a.ContentVersion.SkillId,
                Title = a.ContentVersion.Title, Description = a.ContentVersion.Description, ActivityType = a.ActivityType,
                EstimatedDurationMinutes = a.ContentVersion.EstimatedDurationMinutes, Payload = a.ContentVersion.Payload };
        }
        else if (a.ActivityType == "Study")
        {
            var node = await db.KnowledgeNodes.AsNoTracking().SingleAsync(n => n.CenterId == a.CenterId && n.NodeId == a.SessionRun.Session.TopicNodeId, ct);
            var qid = a.IssuedItems.FirstOrDefault()?.QuestionId;
            var example = qid.HasValue ? await db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.CenterId == a.CenterId && q.QuestionId == qid && q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active, ct) : null;
            result.Content = new ActivityContentDto { Title = node.NodeName, SkillId = node.NodeId.ToString(), ActivityType = "Study",
                Description = node.Description, Payload = new { prompt = "Em rút ra điều gì từ nội dung này?", example = example?.QuestionText,
                    solution = example?.Solution, notice = example == null ? "Chưa có ví dụ kèm lời giải đã xuất bản cho kỹ năng này." : null } };
        }
        else
        {
            var item = a.IssuedItems.SingleOrDefault();
            if (item?.QuestionId != null && questions != null)
                result.Question = await questions.GetQuestionDtoAsync(a.CenterId, a.SessionRun.SessionId, item.QuestionId.Value, a.ActivityType != "Practice", ct)
                    ?? throw new InvalidOperationException("Câu hỏi đã bị thu hồi. Bài làm trước đó được giữ; cần giáo viên bổ sung nội dung.");
        }
        return result;
    }
}
