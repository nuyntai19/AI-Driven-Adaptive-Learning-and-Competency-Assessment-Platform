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
    ISubmitAttemptUseCase? submitAttempt = null, ILearningSessionExecutionService? questions = null,
    ILearningTutorFeedbackGenerator? tutor = null) : ILearningActivityExecutionService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<MissionRunDto> GetMissionRunAsync(Guid centerId, Guid studentId, Guid missionId, ulong sessionRunId, CancellationToken cancellationToken = default)
    {
        await LearningPathV4Data.SessionAsync(db, centerId, studentId, missionId, cancellationToken);
        var run = await db.LearningPathSessionRuns.Include(r => r.Session).SingleOrDefaultAsync(r => r.CenterId == centerId &&
            r.StudentId == studentId && r.SessionId == missionId && r.SessionRunId == sessionRunId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lượt học thuộc nhiệm vụ của bạn.");
        return await RunDtoAsync(run, cancellationToken);
    }

    public Task<MissionRunDto> StartMissionAsync(Guid centerId, Guid studentId, Guid missionId, CancellationToken cancellationToken = default)
        => StartMissionAsync(centerId, studentId, missionId, new StartMissionRequest(), cancellationToken);

    public async Task<MissionRunDto> StartMissionAsync(Guid centerId, Guid studentId, Guid missionId, StartMissionRequest request, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await StudentLockHelper.AcquireStudentLockAsync(db, centerId, studentId, cancellationToken);
        var session = await LearningPathV4Data.SessionAsync(db, centerId, studentId, missionId, cancellationToken);
        var run = await db.LearningPathSessionRuns.Include(r => r.Session).Where(r => r.CenterId == centerId &&
            r.StudentId == studentId && r.SessionId == missionId).OrderByDescending(r => r.RunNumber).FirstOrDefaultAsync(cancellationToken);
        var lastFinished = await db.LearningPathSessionRuns.Include(r => r.Session).Where(r => r.CenterId == centerId &&
            r.StudentId == studentId && r.SessionId == missionId && r.Status != SessionRunStatus.Abandoned).OrderByDescending(r => r.RunNumber).FirstOrDefaultAsync(cancellationToken);
        if (session.Status == LearningPathSessionStatus.Skipped && lastFinished == null)
            throw new InvalidOperationException("Nhiệm vụ này đã được điều chỉnh sau kết quả mới. Hãy chọn bước trong tuyến hiện tại.");
        // Unfinished work is always resumed, never duplicated.
        if (run != null && run.Status is SessionRunStatus.InProgress or SessionRunStatus.Grading or SessionRunStatus.NeedsReview)
        {
            await tx.CommitAsync(cancellationToken);
            return await RunDtoAsync(run, cancellationToken);
        }
        // A finished mission opens its saved result unless the student explicitly asks for a new run.
        if (lastFinished != null && (!request.Retry || session.Status == LearningPathSessionStatus.Skipped))
        {
            await tx.CommitAsync(cancellationToken);
            return await RunDtoAsync(lastFinished, cancellationToken);
        }
        if (session.LearningPath.Status != LearningPathStatus.Active) throw new InvalidOperationException("Tuyến này đã được thay thế; hãy chọn nhiệm vụ trong tuyến hiện tại.");
        // A short review may run alongside an unfinished set; two unfinished sets of regular work may not.
        var isReview = session.Objective == "Review";
        var ongoing = isReview ? null : await db.LearningPathSessionRuns.Where(r => r.CenterId == centerId && r.StudentId == studentId &&
            r.Session.LearningPath.SubjectId == session.LearningPath.SubjectId && r.SessionId != missionId && r.Status == SessionRunStatus.InProgress &&
            r.Session.Objective != "Review")
            .OrderByDescending(r => r.LastActivityAt)
            .Select(r => new { r.SessionId, r.SessionRunId, r.Session.Title }).FirstOrDefaultAsync(cancellationToken);
        if (ongoing != null)
            throw new LearningMissionInProgressException(ongoing.SessionId, ongoing.SessionRunId, ongoing.Title);
        var node = (await LearningPathV4Data.NodesAsync(db, centerId, studentId, session.LearningPath.SubjectId, session.ClassId, cancellationToken))
            .FirstOrDefault(n => n.NodeId == session.TopicNodeId) ?? throw new InvalidOperationException("Kỹ năng không còn khả dụng.");
        var plan = JsonSerializer.Deserialize<PathProposalDto>(session.LearningPath.PlanJson!.RootElement.GetRawText(), LearningPathV4Data.Json);
        var definition = plan?.Milestones.SelectMany(m => m.Missions).FirstOrDefault(m => m.MissionId == missionId.ToString())
            ?? throw new InvalidOperationException("Nhiệm vụ không có trong bản kế hoạch đã xác nhận.");
        if (!definition.CanStart && definition.Purpose is not ("Practice" or "Review" or "IndependentCheck")) throw new InvalidOperationException(definition.SelectionReason);
        if (definition.Deferred) throw new InvalidOperationException("Nhiệm vụ này đã hoãn vì vượt ngân sách học. Hãy điều chỉnh phạm vi hoặc lịch học trước.");
        if (definition.TimeLimitMinutes.HasValue)
        {
            var completedRuns = await db.LearningPathSessionRuns.Where(r => r.CenterId == centerId && r.StudentId == studentId && r.Session.LearningPathId == session.LearningPathId && r.Status == SessionRunStatus.Completed && r.CompletedAt != null).ToListAsync(cancellationToken);
            var today = LearningPathCalendar.LocalDate(Now, plan!.Schedule.TimeZoneId);
            var definitions = plan.Milestones.SelectMany(m => m.Missions).ToDictionary(m => m.MissionId);
            var spent = completedRuns.Where(r => LearningPathCalendar.LocalDate(r.CompletedAt!.Value, plan.Schedule.TimeZoneId) == today)
                .Sum(r => (long)(definitions.GetValueOrDefault(r.SessionId.ToString())?.EstimatedMinutes ?? 0));
            if (spent + definition.TimeLimitMinutes.Value > plan.AvailableMinutesPerDay)
                throw new InvalidOperationException("Quỹ học còn lại hôm nay chưa đủ cho toàn bộ bài bấm giờ. Hãy làm vào ngày học tiếp theo hoặc điều chỉnh mục tiêu.");
        }
        if (definition.AvailableFrom > Now) throw new InvalidOperationException("Chưa tới ngày ôn của nhiệm vụ này. Bạn có thể tiếp tục các phần khác trong tuyến.");
        var capabilityMap = await LearningPathV4Data.MapAsync(db, centerId, studentId, session.LearningPath.SubjectId, session.ClassId, Now, cancellationToken);
        var capability = capabilityMap.Skills.Single(s => s.SkillId == node.NodeId.ToString());
        if (definition.IsMilestoneExam && !definition.IsProbe)
        {
            var milestone = plan!.Milestones.First(m => m.Missions.Any(x => x.MissionId == definition.MissionId));
            if (capability.State != "Confirmed" && !milestone.ReadyForCheck && await db.LearningPathSessions.AnyAsync(s => s.CenterId == centerId && s.LearningPathId == session.LearningPathId &&
                s.Status != LearningPathSessionStatus.Completed && s.Status != LearningPathSessionStatus.Skipped && s.Objective != "IndependentCheck" && s.TopicNodeId == session.TopicNodeId, cancellationToken))
                throw new InvalidOperationException("Hãy hoàn thành phần học và luyện của chặng trước khi làm mốc kiểm tra.");
        }
        var vouched = LearningGoalScope.Vouched(plan!.SelfReportedPrerequisiteSkillIds, plan.SelfAssessedSkillIds);
        if (definition.Purpose is "Practice" or "Review" && LearningGoalScope.NeedsExternalAssessment(capability, capabilityMap, vouched))
            throw new InvalidOperationException("Cần kiểm tra trực tiếp kỹ năng đích hoặc áp dụng giáo trình bổ trợ cho nền tảng ngoài phạm vi trước khi luyện tiếp.");
        if (lastFinished != null && definition.Purpose == "IndependentCheck")
        {
            if (plan!.IsPlacement) throw new InvalidOperationException("Phần kiểm tra đầu vào đã hoàn thành. Hãy lập tuyến luyện từ kết quả; bài đã làm được giữ nguyên.");
            if (lastFinished.Outcome == SessionRunOutcome.Passed || session.Status == LearningPathSessionStatus.Completed)
                throw new InvalidOperationException("Kỹ năng đã đạt kiểm tra độc lập. Lịch ôn sẽ kiểm tra lại khả năng nhớ; không cần làm lại ngay.");
            // Join through the session instead of a Guid list (not translatable on MySQL).
            var repaired = await db.LearningPathSessionRuns.AnyAsync(r => r.CenterId == centerId && r.StudentId == studentId &&
                r.Session.LearningPathId == session.LearningPathId && r.Session.TopicNodeId == session.TopicNodeId &&
                (r.Session.Objective == "Practice" || r.Session.Objective == "Review") &&
                r.Status == SessionRunStatus.Completed && r.CompletedAt > lastFinished.CompletedAt, cancellationToken);
            if (!repaired && !definition.IsExternalPrerequisiteAssessment) throw new InvalidOperationException("Hãy hoàn thành một bộ rèn lại của kỹ năng này trước khi kiểm tra lại bằng câu mới.");
        }
        var assessedNodes = definition.AssessedSkillIds.Count > 0 ? definition.AssessedSkillIds.Select(ulong.Parse).ToArray() : new[] { node.NodeId };
        var pool = await LearningPathV4Data.QuestionsAsync(db, centerId, studentId, session.LearningPath.SubjectId, assessedNodes, session.ClassId, cancellationToken);
        var independent = definition.Purpose == "IndependentCheck";
        if (independent)
        {
            var prerequisitesReady = definition.IsExternalPrerequisiteAssessment
                ? LearningGoalScope.PrerequisitesSatisfied(capability, capabilityMap, vouched) || LearningGoalScope.CanAssessTargetDirectly(capability, capabilityMap, vouched)
                : plan.IsPlacement || definition.IsProbe || LearningGoalScope.PrerequisitesSatisfied(capability, capabilityMap, vouched);
            if (!prerequisitesReady)
                throw new InvalidOperationException("Cần kiểm tra các kỹ năng nền tảng trước khi xác nhận kỹ năng này.");
            // Any question already answered or shown as a worked example is unsuitable for a new independent check.
            var answered = await db.Attempts.Where(a => a.CenterId == centerId && a.StudentId == studentId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken);
            var exposed = await db.IssuedItems.Where(i => i.CenterId == centerId && i.ActivityRun.SessionRun.StudentId == studentId &&
                i.SolutionExposedAt != null && i.QuestionId != null).Select(i => i.QuestionId!.Value).Distinct().ToListAsync(cancellationToken);
            var reserved = await db.IssuedItems.Where(i => i.CenterId == centerId && i.ActivityRun.SessionRun.StudentId == studentId &&
                i.QuestionId != null && i.ActivityRun.SessionRun.Status == SessionRunStatus.InProgress)
                .Select(i => i.QuestionId!.Value).Distinct().ToListAsync(cancellationToken);
            pool = pool.Where(q => !answered.Contains(q.QuestionId) && !exposed.Contains(q.QuestionId) && !reserved.Contains(q.QuestionId)).ToList();
            var minimum = definition.IsMilestoneExam ? 5 * Math.Max(1, definition.AssessedSkillIds.Count) : plan.IsPlacement || definition.IsProbe ? LearningAssessmentPolicy.MinimumPlacementQuestions : LearningAssessmentPolicy.DesiredQuestions;
            if (pool.Count < minimum) throw new InvalidOperationException($"Có {pool.Count}/{minimum} câu mới đủ điều kiện. Phần này chưa đủ dữ liệu; vẫn có thể học các phần khác hoặc lập lộ trình từ dữ liệu hiện có.");
            if (definition.IsMilestoneExam && assessedNodes.Any(id => pool.Count(q => q.PrimaryTopicNodeId == id) < 5))
                throw new InvalidOperationException("Một phần thi thử chưa đủ 5 câu mới. Hãy làm các phần khác hoặc cập nhật phạm vi thi thử.");
        }
        var number = (run?.RunNumber ?? 0) + 1;
        run = new LearningPathSessionRun
        {
            CenterId = centerId, StudentId = studentId, SessionId = missionId, Session = session, RunNumber = number,
            Status = SessionRunStatus.InProgress, Outcome = SessionRunOutcome.None, StartedAt = Now, LastActivityAt = Now,
            CreatedAt = Now, UpdatedAt = Now, RowVersion = 1,
            PolicySnapshot = JsonSerializer.SerializeToDocument(new { minimumIndependentQuestions = !definition.IsMilestoneExam && (plan.IsPlacement || definition.IsProbe) ? 3 : 5,
                isPlacement = plan.IsPlacement, classificationPolicy = LearningAssessmentPolicy.Version,
                isMilestoneExam = definition.IsMilestoneExam, timeLimitMinutes = definition.TimeLimitMinutes,
                requiredCorrectRate = .8m, type = definition.Purpose, version = LearningPathV4Data.Policy })
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
            // Up to two worked examples: answered questions first, unseen ones only when the bank still keeps 5 for the check.
            var examples = StudyMaterial.Examples(pool, used);
            if (string.IsNullOrWhiteSpace(node.Description) && examples.Count == 0)
                throw new InvalidOperationException(StudyMaterial.MissingMessage);
            Add("Study", questionId: examples.FirstOrDefault()?.QuestionId);
            foreach (var extra in examples.Skip(1))
                activities.Last().IssuedItems.Add(new IssuedItem { CenterId = centerId, QuestionId = extra.QuestionId, ItemOrder = 2, ExposureMode = "SolutionExposed",
                    SolutionExposedAt = Now, IssuedAt = Now, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
        }
        else
        {
            if (!independent)
            {
                var used = await db.Attempts.Where(a => a.CenterId == centerId && a.StudentId == studentId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken);
                pool = definition.DifficultyFloor.HasValue ? pool.Where(q => q.Difficulty >= definition.DifficultyFloor).ToList()
                    : LearningPathInsights.PracticePool(pool, used.ToHashSet());
                var recent = await db.Attempts.AsNoTracking().Where(a => a.CenterId == centerId && a.StudentId == studentId &&
                    a.Question.PrimaryTopicNodeId == node.NodeId).OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
                var wrong = recent.GroupBy(a => a.QuestionId).Where(g => g.First().IsCorrect == false).Select(g => g.Key).ToHashSet();
                // Prioritize previously missed items, then increase difficulty through the set.
                // Previously missed items first, then unseen items, then items already answered correctly; easy to hard inside each group.
                // The loop's target difficulty decides the level inside each group (harder after a good set, easier after a weak one).
                var target = definition.TargetDifficulty;
                pool = pool.OrderBy(q => wrong.Contains(q.QuestionId) ? 0 : used.Contains(q.QuestionId) ? 2 : 1)
                    .ThenBy(q => target.HasValue ? Math.Abs(q.Difficulty - target.Value) : q.Difficulty).ThenBy(q => q.Difficulty).ThenBy(q => q.QuestionId).ToList();
            }
            if (pool.Count == 0) throw new InvalidOperationException("Chưa có câu hỏi hợp lệ cho nhiệm vụ này.");
            // Today's plan may resize a practice set to the chosen time; the independent check stays fixed for comparability.
            var requested = request.QuestionCount is > 0 ? request.QuestionCount.Value : definition.QuestionCount;
            var size = requested > 0 ? (int)Math.Clamp(requested, 1u, LearningPathPacing.MaxPracticeQuestions) : 6;
            var picked = independent ? definition.IsMilestoneExam && assessedNodes.Length > 1
                ? assessedNodes.SelectMany(id => MixedCheck(pool.Where(q => q.PrimaryTopicNodeId == id).ToList())).ToList() : MixedCheck(pool) : pool.Take(size).ToList();
            foreach (var q in picked) Add(independent ? "IndependentCheck" : "Practice", questionId: q.QuestionId);
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

    /// <summary>Five unseen questions with at least two of medium-or-higher difficulty when the bank has them, ordered easy to hard.</summary>
    private static List<DAL.CurriculumAndQuestions.Question> MixedCheck(List<DAL.CurriculumAndQuestions.Question> pool)
    {
        var ordered = pool.OrderBy(q => q.Difficulty).ThenBy(q => q.QuestionId).ToList();
        var size = Math.Min(5, ordered.Count);
        var harder = ordered.Where(q => q.Difficulty >= 3).Take(Math.Min(2, size)).ToList();
        var pick = harder.Concat(ordered.Where(q => !harder.Contains(q)).Take(size - harder.Count)).ToList();
        return pick.OrderBy(q => q.Difficulty).ThenBy(q => q.QuestionId).ToList();
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
        if (LearningAssessmentPolicy.ExpiresAt(activity.SessionRun.PolicySnapshot, activity.SessionRun.StartedAt) is { } expires && Now >= expires)
            throw new InvalidOperationException("Đã hết giờ làm bài. Hãy xem kết quả; các câu đã nộp vẫn được giữ.");
        if (activity.SessionRun.Status != SessionRunStatus.InProgress || activity.Status == "Completed")
            throw new InvalidOperationException("Hoạt động đã nộp hoặc đang chờ đánh giá; không thể sửa.");
        if (await db.ActivityRuns.AnyAsync(a => a.CenterId == activity.CenterId && a.SessionRunId == activity.SessionRunId &&
            a.ActivityOrder < activity.ActivityOrder && a.Status != "Completed" && a.Status != "Submitted", ct))
            throw new InvalidOperationException("Hãy hoàn thành hoạt động trước đó.");
    }

    public async Task<ActivityRunDto> GetActivityRunAsync(Guid centerId, Guid studentId, ulong activityRunId, CancellationToken cancellationToken = default)
        => await DtoAsync(await LoadAsync(centerId, studentId, activityRunId, cancellationToken), cancellationToken);

    private static LearningTutorFeedbackDto? ReadTutor(ActivityRun activity)
    {
        if (activity.Feedback?.StartsWith("{", StringComparison.Ordinal) != true) return null;
        try { var value = JsonSerializer.Deserialize<LearningTutorFeedbackDto>(activity.Feedback, LearningPathV4Data.Json);
            return value?.Status == "Generated" ? value : null; } catch (JsonException) { return null; }
    }

    public async Task<LearningTutorFeedbackDto> GetAiFeedbackAsync(Guid centerId, Guid studentId, ulong activityRunId, CancellationToken cancellationToken = default)
    {
        var activity = await LoadAsync(centerId, studentId, activityRunId, cancellationToken);
        if (activity.Status is not ("Submitted" or "Completed")) throw new InvalidOperationException("Hãy nộp phần trả lời trước khi xem nhận xét AI.");
        if (activity.ActivityType is "IndependentCheck" or "TransferChallenge" && await db.ActivityRuns.AnyAsync(a => a.CenterId == centerId &&
            a.SessionRunId == activity.SessionRunId && a.Status != "Submitted" && a.Status != "Completed", cancellationToken))
            throw new InvalidOperationException("Chỉ xem nhận xét và lời giải AI sau khi đã nộp cả bộ tự kiểm tra.");
        var cached = ReadTutor(activity); if (cached != null) return cached;
        if (tutor == null) return new() { Comment = "Chưa cấu hình trợ giảng AI. Bài làm và lời giải tham khảo vẫn được giữ." };
        var dto = await DtoAsync(activity, cancellationToken);
        var topic = await db.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == centerId && n.NodeId == activity.SessionRun.Session.TopicNodeId)
            .Select(n => n.NodeName).SingleAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(dto.Content?.Payload, LearningPathV4Data.Json);
        var answer = dto.SubmittedResponse ?? dto.LearningFeedback?.SubmittedAnswer ?? "";
        var context = new LearningTutorContext(activity.ActivityType, topic, dto.Question?.Question?.QuestionText ?? ((dto.Content?.Description ?? "") + "\n" + payload),
            dto.LearningFeedback?.Explanation ?? payload, answer, dto.LearningFeedback == null ? null : activity.IssuedItems.FirstOrDefault()?.Attempt?.ReasoningText,
            dto.LearningFeedback?.IsCorrect);
        var result = await tutor.GenerateAsync(context, cancellationToken);
        if (result.Status != "Generated") return result;
        var serialized = JsonSerializer.Serialize(result, LearningPathV4Data.Json);
        if (System.Text.Encoding.UTF8.GetByteCount(serialized) > 60000) return new() { Comment = "Nhận xét AI quá dài để lưu. Hãy thử lại; bài làm vẫn được giữ." };
        activity.Feedback = serialized; activity.RowVersion++; activity.UpdatedAt = Now;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { await db.Entry(activity).ReloadAsync(cancellationToken); return ReadTutor(activity) ?? result; }
        return result;
    }

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
        if (a.ActivityType is "IndependentCheck" or "TransferChallenge")
        {
            a.AssistanceRequestedCount++; a.RowVersion++; a.UpdatedAt = Now;
            await db.SaveChangesAsync(cancellationToken);
            return new AssistanceResponseDto { IsBlocked = true, AssistanceMode = "Blocked", FallbackUsed = true,
                Explanation = "Phần tự kiểm tra không cung cấp gợi ý hoặc lời giải trước khi nộp.",
                PolicyNotice = "Trợ giúp bị chặn ở phía server trong kiểm tra độc lập." };
        }
        var hints = await HintLadderAsync(a, cancellationToken);
        var index = (int)Math.Min(request.StepIndex, (uint)(hints.Count - 1));
        // Only a newly revealed level is counted; re-opening an earlier hint (e.g. after reload) is free.
        if (index + 1 > a.AssistanceRequestedCount)
        {
            a.AssistanceRequestedCount = (uint)index + 1; a.AssistanceMode = "HintOnly"; a.RowVersion++; a.UpdatedAt = Now;
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { await db.Entry(a).ReloadAsync(cancellationToken); }
        }
        var (title, text, authored) = hints[index];
        return new AssistanceResponseDto
        {
            AssistanceMode = "HintOnly", HintTitle = title, Hint = title, Explanation = text, FallbackUsed = !authored,
            HintLevel = (uint)index + 1, TotalHints = (uint)hints.Count, HasMoreHints = index + 1 < hints.Count,
            PolicyNotice = hints.Count == 1 && !authored
                ? "Câu này chưa có lời giải được biên soạn nên chỉ có gợi ý định hướng. Hệ thống không tự đoán lời giải."
                : "Gợi ý mở từng bước và không đưa đáp án. Câu có dùng gợi ý vẫn được tính là luyện có hướng dẫn."
        };
    }

    /// <summary>
    /// Progressive hints built only from authored data: approach (expected reasoning / skill notes), then the first part of the
    /// published solution, then the solution without its conclusion. The correct answer is masked at every level.
    /// </summary>
    private async Task<List<(string Title, string Text, bool Authored)>> HintLadderAsync(ActivityRun a, CancellationToken ct)
    {
        var node = await db.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == a.CenterId && n.NodeId == a.SessionRun.Session.TopicNodeId)
            .Select(n => new { n.NodeName, n.Description }).FirstOrDefaultAsync(ct);
        var skillName = node?.NodeName ?? "kỹ năng này";
        var notes = string.IsNullOrWhiteSpace(node?.Description) ? null : Shorten(node!.Description!, 500);
        var hints = new List<(string, string, bool)>();
        var questionId = a.IssuedItems.FirstOrDefault(i => i.QuestionId != null)?.QuestionId;
        var question = questionId == null ? null : await db.Questions.AsNoTracking()
            .FirstOrDefaultAsync(q => q.CenterId == a.CenterId && q.QuestionId == questionId, ct);
        if (question != null && a.ActivityType != "Study")
        {
            var answer = question.CorrectAnswer?.Trim() ?? "";
            var approach = !string.IsNullOrWhiteSpace(question.ExpectedReasoning) ? Mask(question.ExpectedReasoning!, answer) : null;
            hints.Add(("Định hướng", approach != null
                ? approach
                : notes != null ? $"Câu này thuộc {skillName}. Nhắc lại kiến thức: {notes}\nHãy xác định câu hỏi yêu cầu gì và quy tắc nào áp dụng trước khi trả lời."
                : $"Câu này thuộc {skillName}. Gạch chân dữ kiện quan trọng, xác định câu hỏi yêu cầu gì và viết ra quy tắc em định dùng trước khi trả lời.",
                approach != null || notes != null));
            var steps = SplitSteps(question.Solution ?? "");
            // The last step of a multi-step solution usually states the result; it is never shown before submission.
            if (steps.Count >= 2) steps.RemoveAt(steps.Count - 1);
            steps = steps.Select(st => Mask(st, answer)).Where(st => !string.IsNullOrWhiteSpace(st)).ToList();
            if (steps.Count >= 1)
                hints.Add(("Bước đầu tiên", string.Join("\n", steps.Take(Math.Max(1, (steps.Count + 2) / 3))), true));
            if (steps.Count >= 2)
                hints.Add(("Gần tới đáp án", string.Join("\n", steps) + "\nTự hoàn thành bước cuối và kiểm tra lại kết quả.", true));
            // One-word answers and multiple choice: a third hint would effectively be the answer, so stop at two.
            var shortAnswer = question.QuestionType == Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice ||
                (answer.Length <= 20 && !answer.Any(char.IsDigit) && answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 2);
            if (shortAnswer && hints.Count > 2) hints.RemoveRange(2, hints.Count - 2);
            return hints;
        }
        var content = a.ContentVersion?.Description;
        hints.Add(("Định hướng", notes != null ? $"Ý chính của {skillName}: {notes}" : !string.IsNullOrWhiteSpace(content) ? Shorten(content!, 500)
            : "Chưa có phần lý thuyết hoặc ví dụ để đọc lại. Hãy yêu cầu giáo viên bổ sung học liệu cho kỹ năng này.", notes != null || !string.IsNullOrWhiteSpace(content)));
        hints.Add(a.ActivityType == "FindError"
            ? ("Cách tìm lỗi", "Đi lần lượt từng bước: bước này dùng quy tắc nào, quy tắc đó có áp dụng đúng điều kiện không? Bước đầu tiên không thỏa là bước sai.", false)
            : ("Cách trình bày", "Viết 2–3 câu: (1) quy tắc là gì, (2) khi nào dùng, (3) một ví dụ của riêng em. Không cần chép lại nguyên văn bài học.", false));
        return hints;
    }

    private static List<string> SplitSteps(string text)
    {
        var lines = text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (lines.Count == 1 && lines[0].Length > 160)
            lines = System.Text.RegularExpressions.Regex.Split(lines[0], @"(?<=[.!?;])\s+").Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
        return lines;
    }

    private static string Mask(string text, string answer)
    {
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 200) return text;
        // "x = 2" is also leaked by "2" alone; mask the right-hand side as well.
        var eq = answer.LastIndexOf('=');
        if (eq >= 0 && eq < answer.Length - 1) text = MaskOne(text, answer[(eq + 1)..].Trim());
        return MaskOne(text, answer);
    }

    private static string MaskOne(string text, string answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return text;
        var escaped = System.Text.RegularExpressions.Regex.Escape(answer);
        var wordLike = System.Text.RegularExpressions.Regex.IsMatch(answer, @"^[\p{L}\p{N}][\p{L}\p{N}\s'-]*$");
        var pattern = wordLike ? $@"(?<![\p{{L}}\p{{N}}]){escaped}(?![\p{{L}}\p{{N}}])" : escaped;
        // One-character answers (e.g. option "A") are matched case-sensitively so articles like "a" survive.
        var options = answer.Length <= 1 ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        return System.Text.RegularExpressions.Regex.Replace(text, pattern, "___", options);
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max].TrimEnd() + "…";

    public async Task<MissionRunDto> AbandonMissionRunAsync(Guid centerId, Guid studentId, Guid missionId, ulong sessionRunId, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await StudentLockHelper.AcquireStudentLockAsync(db, centerId, studentId, cancellationToken);
        await LearningPathV4Data.SessionAsync(db, centerId, studentId, missionId, cancellationToken);
        var run = await db.LearningPathSessionRuns.Include(r => r.Session).SingleOrDefaultAsync(r => r.CenterId == centerId &&
            r.StudentId == studentId && r.SessionId == missionId && r.SessionRunId == sessionRunId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy lượt học thuộc nhiệm vụ của bạn.");
        if (run.Status != SessionRunStatus.InProgress)
            throw new InvalidOperationException("Chỉ hủy được lượt đang làm. Lượt đang chấm hoặc đã xong được giữ nguyên.");
        var activities = await db.ActivityRuns.Where(x => x.CenterId == centerId && x.SessionRunId == sessionRunId).ToListAsync(cancellationToken);
        var submitted = activities.Count(x => x.Status is "Submitted" or "Completed");
        // A started independent check must be finished: cancelling after weak answers would make the check meaningless.
        if (submitted > 0 && activities.Any(x => x.ActivityType is "IndependentCheck" or "TransferChallenge"))
            throw new InvalidOperationException("Bài tự kiểm tra đã có câu được nộp nên không hủy giữa chừng để kết quả công bằng. Em có thể tạm dừng và làm tiếp sau.");
        run.Status = SessionRunStatus.Abandoned; run.Outcome = SessionRunOutcome.None; run.CompletedAt = Now;
        run.LastActivityAt = Now; run.UpdatedAt = Now; run.RowVersion++;
        var previous = await db.LearningPathSessionRuns.Where(r => r.CenterId == centerId && r.StudentId == studentId && r.SessionId == missionId &&
            r.SessionRunId != sessionRunId && r.Status == SessionRunStatus.Completed).OrderByDescending(r => r.RunNumber).FirstOrDefaultAsync(cancellationToken);
        run.Session.Status = previous == null ? LearningPathSessionStatus.NotStarted
            : previous.Outcome == SessionRunOutcome.NotPassed ? LearningPathSessionStatus.NeedsReview : LearningPathSessionStatus.Completed;
        run.Session.CurrentRunId = previous?.SessionRunId; run.Session.UpdatedAt = Now; run.Session.RowVersion++;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await RunDtoAsync(run, cancellationToken);
    }

    public async Task<ActivitySubmissionResponseDto> SubmitActivityAsync(Guid centerId, Guid studentId, ulong activityRunId, SubmitActivityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ClientSubmissionId == Guid.Empty || request.FinalAnswer.Length > 20000 || request.ReasoningText?.Length > 50000)
            throw new ArgumentException("Thiếu mã lần nộp hoặc nội dung vượt giới hạn.");
        var a = await LoadAsync(centerId, studentId, activityRunId, cancellationToken);
        // Study activities may carry two worked examples; only practice/check items hold a single answerable question.
        var issued = a.ActivityType == "Study" ? a.IssuedItems.OrderBy(i => i.ItemOrder).FirstOrDefault() : a.IssuedItems.SingleOrDefault();
        if (issued?.Attempt != null)
        {
            if (issued.Attempt.ClientSubmissionId != request.ClientSubmissionId || request.QuestionId != issued.QuestionId ||
                issued.Attempt.FinalAnswer != request.FinalAnswer || issued.Attempt.ReasoningText != request.ReasoningText)
                throw new InvalidOperationException("Hoạt động đã nộp với nội dung khác.");
            await AppendPrerequisiteProbeAsync(a, issued.AttemptId!.Value, cancellationToken);
            return await SubmissionResponseAsync(a, issued.AttemptId, cancellationToken);
        }
        if (a.Status == "Completed")
        {
            var previous = JsonSerializer.Deserialize<SubmitActivityRequest>(a.DraftPayload ?? "{}", LearningPathV4Data.Json);
            if (previous?.ClientSubmissionId != request.ClientSubmissionId || previous.FinalAnswer != request.FinalAnswer || previous.SelectedErrorStep != request.SelectedErrorStep)
                throw new InvalidOperationException("Hoạt động đã nộp với nội dung khác.");
            return Response(a, null, a.IsMastered, ReadTutor(a)?.Comment ?? a.Feedback ?? "Đã lưu.");
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
            await AppendPrerequisiteProbeAsync(a, ulong.Parse(result.Data!.AttemptId), cancellationToken);
            // The canonical submit transaction links IssuedItem + ActivityRun before committing.
            return await SubmissionResponseAsync(a, ulong.Parse(result.Data!.AttemptId), cancellationToken);
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

    private async Task AppendPrerequisiteProbeAsync(ActivityRun activity, ulong attemptId, CancellationToken ct)
    {
        var run = activity.SessionRun;
        if (run.PolicySnapshot?.RootElement.TryGetProperty("isPlacement", out var placement) != true || placement.ValueKind != JsonValueKind.True) return;
        var attempt = await db.Attempts.AsNoTracking().FirstAsync(a => a.CenterId == activity.CenterId && a.AttemptId == attemptId, ct);
        if (attempt.IsCorrect != false) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StudentLockHelper.AcquireStudentLockAsync(db, activity.CenterId, run.StudentId, ct);
        if (await db.IssuedItems.AnyAsync(i => i.CenterId == activity.CenterId && i.ActivityRun.SessionRunId == run.SessionRunId &&
            i.QuestionId != null && db.Questions.Any(q => q.CenterId == activity.CenterId && q.QuestionId == i.QuestionId && q.PrimaryTopicNodeId != run.Session.TopicNodeId), ct)) return;
        var map = await LearningPathV4Data.MapAsync(db, activity.CenterId, run.StudentId, run.Session.LearningPath.SubjectId, run.Session.ClassId, Now, ct);
        var prerequisites = map.Skills.FirstOrDefault(s => s.SkillId == run.Session.TopicNodeId.ToString())?.PrerequisiteSkillIds ?? [];
        var eligibleNodes = map.Skills.Where(s => prerequisites.Contains(s.SkillId)).Select(s => ulong.Parse(s.SkillId)).ToList();
        if (eligibleNodes.Count == 0) return;
        var pool = await LearningPathV4Data.QuestionsAsync(db, activity.CenterId, run.StudentId, run.Session.LearningPath.SubjectId, eligibleNodes, run.Session.ClassId, ct);
        var used = await db.Attempts.Where(a => a.CenterId == activity.CenterId && a.StudentId == run.StudentId).Select(a => a.QuestionId).ToListAsync(ct);
        var issued = await db.IssuedItems.Where(i => i.CenterId == activity.CenterId && i.ActivityRun.SessionRun.StudentId == run.StudentId && i.QuestionId != null)
            .Select(i => i.QuestionId!.Value).ToListAsync(ct);
        var selected = pool.Where(q => !used.Contains(q.QuestionId) && !issued.Contains(q.QuestionId)).Take(2).ToList();
        if (selected.Count == 0) return;
        var order = await db.ActivityRuns.Where(a => a.CenterId == activity.CenterId && a.SessionRunId == run.SessionRunId).MaxAsync(a => a.ActivityOrder, ct);
        foreach (var q in selected)
            db.ActivityRuns.Add(new ActivityRun { CenterId = activity.CenterId, SessionRunId = run.SessionRunId, ActivityOrder = ++order,
                ActivityType = "IndependentCheck", Status = "InProgress", StartedAt = Now, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1, DraftRevision = 1,
                IssuedItems = [new IssuedItem { CenterId = activity.CenterId, QuestionId = q.QuestionId, ItemOrder = 1, ExposureMode = "Independent", IssuedAt = Now, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 }] });
        var assigned = LearningSessionExecutionService.ParseQuestionIds(run.QuestionsAssigned);
        assigned.AddRange(selected.Select(q => q.QuestionId)); run.QuestionsAssigned = JsonSerializer.SerializeToDocument(assigned);
        run.TotalQuestions = (uint)assigned.Count; run.CurrentQuestionId ??= selected[0].QuestionId; run.RowVersion++; run.UpdatedAt = Now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task<ActivitySubmissionResponseDto> SubmissionResponseAsync(ActivityRun a, ulong? attempt, CancellationToken ct)
    {
        var feedback = await FeedbackAsync(a, ct);
        var result = Response(a, attempt, feedback?.IsCorrect, feedback?.Explanation ?? "Đã lưu câu trả lời. Phần tự kiểm tra trả lời giải khi nộp xong cả bộ.");
        result.LearningFeedback = feedback;
        result.IsRunFinished = !await db.ActivityRuns.AnyAsync(x => x.CenterId == a.CenterId && x.SessionRunId == a.SessionRunId && x.Status != "Submitted" && x.Status != "Completed", ct);
        return result;
    }

    private async Task<QuestionLearningFeedbackDto?> FeedbackAsync(ActivityRun a, CancellationToken ct)
    {
        var item = a.IssuedItems.FirstOrDefault();
        if (item?.AttemptId == null || item.QuestionId == null) return null;
        if (a.ActivityType != "Practice" && a.SessionRun.Status != SessionRunStatus.Completed && await db.ActivityRuns.AnyAsync(x => x.CenterId == a.CenterId && x.SessionRunId == a.SessionRunId &&
            x.Status != "Completed" && x.Status != "Submitted", ct)) return null;
        var attempt = await db.Attempts.AsNoTracking().SingleAsync(x => x.CenterId == a.CenterId && x.AttemptId == item.AttemptId, ct);
        var question = await db.Questions.AsNoTracking().SingleAsync(q => q.CenterId == a.CenterId && q.QuestionId == item.QuestionId, ct);
        var analysis = await db.ReasoningAnalyses.AsNoTracking().FirstOrDefaultAsync(x => x.CenterId == a.CenterId && x.AttemptId == item.AttemptId, ct);
        var correct = analysis?.OverrideIsCorrect ?? attempt.IsCorrect;
        var options = await db.QuestionOptions.AsNoTracking().Where(o => o.CenterId == a.CenterId && o.QuestionId == question.QuestionId).ToListAsync(ct);
        var chosen = options.FirstOrDefault(o => o.OptionId.ToString() == attempt.FinalAnswer || o.OptionLabel == attempt.FinalAnswer);
        var answer = string.Join("; ", options.Where(o => o.IsCorrect).Select(o => $"{o.OptionLabel}. {o.OptionText}"));
        var explanation = question.Solution;
        if (chosen != null && correct == false && question.GradingCriteria.OptionMisconceptions.TryGetValue(chosen.OptionId.ToString(), out var note))
            explanation = note + "\n\n" + explanation;
        return new QuestionLearningFeedbackDto { IsCorrect = correct, SubmittedAnswer = chosen == null ? attempt.FinalAnswer : $"{chosen.OptionLabel}. {chosen.OptionText}",
            Status = analysis?.NeedsTeacherReview == true && analysis.ReviewDecision == null ? "PendingTeacherReview" :
                attempt.Status == AttemptStatus.Completed ? "Reviewed" : correct.HasValue ? "Preliminary" : "Pending",
            CorrectAnswer = string.IsNullOrWhiteSpace(answer) ? question.CorrectAnswer : answer, Explanation = explanation,
            MethodGuide = question.ExpectedReasoning ?? "Đối chiếu dữ kiện với từng bước trong lời giải. Chỉ chuyển bước khi giải thích được vì sao áp dụng quy tắc đó.",
            NextStep = correct == false ? "Đọc lại bước khác với cách làm của em, rồi áp dụng vào câu tiếp theo. Nếu vẫn vướng, xem lại ví dụ của kỹ năng này."
                : correct == true ? "Thử câu tiếp theo mà không nhìn lời giải để kiểm tra em có tự áp dụng được không." : "Bài đã lưu. Em có thể xem lời giải và tiếp tục trong khi chờ nhận xét bổ sung." };
    }

    private async Task<MissionRunDto> RunDtoAsync(LearningPathSessionRun run, CancellationToken ct)
    {
        var ids = await db.ActivityRuns.Where(a => a.CenterId == run.CenterId && a.SessionRunId == run.SessionRunId)
            .OrderBy(a => a.ActivityOrder).Select(a => a.ActivityRunId).ToListAsync(ct);
        var result = new MissionRunDto { SessionRunId = run.SessionRunId, SessionId = run.SessionId, Title = run.Session.Title, Status = run.Status.ToString(), Outcome = run.Outcome.ToString(), ExpiresAt = LearningAssessmentPolicy.ExpiresAt(run.PolicySnapshot, run.StartedAt) };
        foreach (var id in ids) result.Activities.Add(await GetActivityRunAsync(run.CenterId, run.StudentId, id, ct));
        return result;
    }

    private async Task<ActivityRunDto> DtoAsync(ActivityRun a, CancellationToken ct)
    {
        var result = new ActivityRunDto { ActivityRunId = a.ActivityRunId, SessionRunId = a.SessionRunId, ActivityOrder = a.ActivityOrder,
            ActivityType = a.ActivityType, Status = a.Status, DraftPayload = a.DraftPayload, DraftRevision = a.DraftRevision,
            AssistanceRequestedCount = a.AssistanceRequestedCount, AssistanceMode = a.AssistanceMode, Feedback = ReadTutor(a)?.Comment ?? a.Feedback,
            StartedAt = a.StartedAt, CompletedAt = a.CompletedAt };
        result.Stage = a.ActivityType == "Practice" ? a.ActivityOrder <= 2 ? "Khởi động" : a.ActivityOrder <= 4 ? "Gỡ vướng và áp dụng" : "Luyện phản xạ" : a.ActivityType == "IndependentCheck" ? "Tự kiểm tra" : "Tìm hiểu và ví dụ";
        result.LearningFeedback = await FeedbackAsync(a, ct);
        result.AiFeedback = ReadTutor(a);
        if (a.Status is "Submitted" or "Completed")
        {
            result.SubmittedResponse = a.IssuedItems.FirstOrDefault()?.Attempt?.FinalAnswer;
            if (result.SubmittedResponse == null && a.DraftPayload != null)
                try { using var draft = JsonDocument.Parse(a.DraftPayload); if (draft.RootElement.TryGetProperty("finalAnswer", out var value)) result.SubmittedResponse = value.GetString(); } catch (JsonException) { }
        }
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
            var qids = a.IssuedItems.OrderBy(i => i.ItemOrder).Where(i => i.QuestionId.HasValue).Select(i => i.QuestionId!.Value).ToList();
            var found = await db.Questions.AsNoTracking().Where(q => q.CenterId == a.CenterId && qids.Contains(q.QuestionId) &&
                q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active).ToListAsync(ct);
            var ordered = qids.Select(id => found.FirstOrDefault(q => q.QuestionId == id)).Where(q => q != null).Select(q => q!).ToList();
            var example = ordered.FirstOrDefault();
            // Worked examples are shown step by step; the student reveals each step before seeing the next.
            var workedExamples = ordered.Select(q => new { question = q.QuestionText, steps = SplitSteps(q.Solution ?? ""), answer = q.CorrectAnswer }).ToList();
            result.Content = new ActivityContentDto { Title = node.NodeName, SkillId = node.NodeId.ToString(), ActivityType = "Study",
                Description = node.Description, Payload = new { prompt = "Tóm tắt bằng lời của em: quy tắc là gì, khi nào dùng?", example = example?.QuestionText,
                    solution = example?.Solution, examples = workedExamples,
                    notice = example == null ? "Chưa có ví dụ kèm lời giải đã xuất bản cho kỹ năng này." : null } };
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
