using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.UseCases;

public interface IGenerateLearningPathUseCase
{
    Task<DetailedLearningPathDto> ExecuteAsync(GenerateLearningPathRequest request, CancellationToken cancellationToken);
}

public interface IGetDetailedLearningPathUseCase
{
    Task<DetailedLearningPathDto?> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken);
}

public sealed class GenerateLearningPathUseCase : IGenerateLearningPathUseCase, IGetDetailedLearningPathUseCase
{
    private static readonly JsonSerializerOptions PlanJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> GenerationLocks = new();
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly ILearningPathPlanEnricher? _planEnricher;

    public GenerateLearningPathUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext, IRecommendationEngine recommendationEngine,
        ILearningPathPlanEnricher? planEnricher = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _recommendationEngine = recommendationEngine;
        _planEnricher = planEnricher;
    }

    public async Task<DetailedLearningPathDto> ExecuteAsync(GenerateLearningPathRequest request, CancellationToken cancellationToken)
    {
        var (centerId, studentId) = ResolveStudent();
        var lockKey = $"{centerId:D}:{studentId:D}:{request.SubjectId:D}";
        var gate = GenerationLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteCoreAsync(centerId, studentId, request, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<DetailedLearningPathDto> ExecuteCoreAsync(Guid centerId, Guid studentId, GenerateLearningPathRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var now = DateTime.UtcNow;
        var subject = await _dbContext.Subjects.AsNoTracking()
            .SingleOrDefaultAsync(s => s.CenterId == centerId && s.SubjectId == request.SubjectId && !s.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Môn học không tồn tại.");

        var requestedIds = request.WeakTopicNodeIds.Concat(request.FocusTopicNodeIds).Distinct().ToList();
        var validTopicIds = await _dbContext.KnowledgeNodes.AsNoTracking()
            .Where(n => n.CenterId == centerId && n.SubjectId == request.SubjectId && !n.IsDeleted && n.IsActive && requestedIds.Contains(n.NodeId))
            .Select(n => n.NodeId).ToListAsync(cancellationToken);
        var validSet = validTopicIds.ToHashSet();
        var weakIds = request.WeakTopicNodeIds.Where(validSet.Contains).Distinct().ToList();
        var focusIds = request.FocusTopicNodeIds.Where(validSet.Contains).Distinct().ToList();

        var preference = await _dbContext.StudentLearningPathPreferences
            .SingleOrDefaultAsync(p => p.CenterId == centerId && p.StudentId == studentId && p.SubjectId == request.SubjectId && !p.IsDeleted, cancellationToken);
        var unchanged = preference is not null && PreferenceMatches(preference, request, weakIds, focusIds);
        var existingPath = await _recommendationEngine.GetActiveLearningPathAsync(centerId, studentId, request.SubjectId, cancellationToken);
        if (!request.ForceRegenerate && unchanged && existingPath?.PlanJson is not null)
            return await BuildDetailedLearningPathAsync(centerId, studentId, subject, preference!, existingPath, cancellationToken);

        preference = UpsertPreference(preference, centerId, studentId, request, weakIds, focusIds, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var generation = await _recommendationEngine.GenerateAndPersistAsync(centerId, studentId, request.SubjectId, null, now, cancellationToken);
        if (generation.Status is RecommendationGenerationStatus.Blocked or RecommendationGenerationStatus.NoCandidate)
            throw new InvalidOperationException(generation.DiagnosticReason ?? "Chưa có đủ dữ liệu Knowledge Graph để tạo lộ trình.");

        var activePath = await _recommendationEngine.GetActiveLearningPathAsync(centerId, studentId, request.SubjectId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể tạo lộ trình học tập.");
        var detailed = await BuildDetailedLearningPathAsync(centerId, studentId, subject, preference, activePath, cancellationToken, true);
        await EnrichPlanAsync(detailed, cancellationToken);
        activePath.PlanJson = JsonSerializer.SerializeToDocument(detailed.Phases, PlanJsonOptions);
        activePath.RecommendationRationale = detailed.RecommendationRationale;
        activePath.PlanSchemaVersion = "2.0";
        activePath.GenerationStatus = "Ready";
        activePath.AdaptationMessage = detailed.AdaptationMessage;
        activePath.UpdatedAt = now;
        activePath.UpdatedBy = studentId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return detailed;
    }

    public async Task<DetailedLearningPathDto?> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var (centerId, studentId) = ResolveStudent();
        var subject = await _dbContext.Subjects.AsNoTracking()
            .SingleOrDefaultAsync(s => s.CenterId == centerId && s.SubjectId == subjectId && !s.IsDeleted, cancellationToken);
        if (subject is null) return null;
        var preference = await _dbContext.StudentLearningPathPreferences.AsNoTracking()
            .SingleOrDefaultAsync(p => p.CenterId == centerId && p.StudentId == studentId && p.SubjectId == subjectId && !p.IsDeleted, cancellationToken);
        // A learning path is durable history. Prefer the active version, but keep showing the
        // latest completed/superseded version when another workflow has changed its state.
        var path = await _dbContext.LearningPaths
            .Include(lp => lp.Items.Where(i => !i.IsDeleted).OrderBy(i => i.RankOrder))
            .Where(lp => lp.CenterId == centerId
                && lp.StudentId == studentId
                && lp.SubjectId == subjectId
                && !lp.IsDeleted)
            .OrderBy(lp => lp.Status == LearningPathStatus.Active ? 0 : lp.Status == LearningPathStatus.Completed ? 1 : 2)
            .ThenByDescending(lp => lp.Version)
            .ThenByDescending(lp => lp.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (path is null || preference is null) return null;
        var detailed = await BuildDetailedLearningPathAsync(centerId, studentId, subject, preference, path, cancellationToken);
        if (path.PlanJson is null)
        {
            path.PlanJson = JsonSerializer.SerializeToDocument(detailed.Phases, PlanJsonOptions);
            path.RecommendationRationale = detailed.RecommendationRationale;
            path.PlanSchemaVersion = "2.0";
            path.GenerationStatus = "Ready";
            path.AdaptationMessage = detailed.AdaptationMessage;
            path.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        return detailed;
    }

    private async Task<DetailedLearningPathDto> BuildDetailedLearningPathAsync(Guid centerId, Guid studentId, DAL.Organization.Subject subject,
        StudentLearningPathPreference preference, LearningPath path, CancellationToken cancellationToken, bool forceRebuild = false)
    {
        var topics = await _dbContext.KnowledgeNodes.AsNoTracking()
            .Where(n => n.CenterId == centerId && n.SubjectId == subject.SubjectId && n.IsActive && !n.IsDeleted)
            .OrderBy(n => n.OrderIndex).ThenBy(n => n.NodeId).ToListAsync(cancellationToken);
        var topicIds = topics.Select(t => t.NodeId).ToList();
        var topicById = topics.ToDictionary(t => t.NodeId);
        var twins = await _dbContext.KnowledgeTwins.AsNoTracking()
            .Where(t => t.CenterId == centerId && t.StudentId == studentId && topicIds.Contains(t.TopicNodeId) && !t.IsDeleted)
            .ToDictionaryAsync(t => t.TopicNodeId, cancellationToken);
        var currentOverall = twins.Count == 0 ? 0m : Math.Round(twins.Values.Average(t => t.MasteryPercentage), 1);

        var itemDetails = path.Items.Where(i => !i.IsDeleted).OrderBy(i => i.RankOrder)
            .Where(i => topicById.ContainsKey(i.TopicNodeId))
            .Select(i =>
            {
                var current = twins.TryGetValue(i.TopicNodeId, out var twin) ? Math.Round(twin.MasteryPercentage, 1) : 0m;
                return new LearningPathItemDetailDto
                {
                    LearningPathItemId = i.LearningPathItemId.ToString(), TopicNodeId = i.TopicNodeId.ToString(),
                    TopicName = topicById[i.TopicNodeId].NodeName, CurrentMastery = current,
                    TargetMastery = CalculateRealisticTarget(current, preference.TargetMastery), RankOrder = i.RankOrder,
                    OpportunityScore = i.OpportunityScore, Reason = i.Reason, Prerequisites = topicById[i.TopicNodeId].Description,
                    EstimatedMinutes = preference.MinutesPerDay, Status = MapItemStatus(i.Status.ToString()),
                    RecommendedQuestionId = i.RecommendedQuestionId?.ToString()
                };
            }).ToList();

        List<LearningPathPhaseDto> phases;
        if (!forceRebuild && path.PlanJson is not null)
            phases = JsonSerializer.Deserialize<List<LearningPathPhaseDto>>(path.PlanJson.RootElement.GetRawText(), PlanJsonOptions) ?? new();
        else
            phases = await BuildScheduleAsync(centerId, subject.SubjectId, path.LearningPathId, itemDetails, preference, cancellationToken);

        var allSessions = phases.SelectMany(p => p.Weeks).SelectMany(w => w.Sessions).ToList();
        var progress = allSessions.Count == 0 ? 0m : Math.Round((decimal)allSessions.Count(s => s.Status == "Completed") / allSessions.Count * 100m, 1);
        var rationale = path.RecommendationRationale ?? BuildRationale(subject.SubjectName, preference, itemDetails);
        var adaptation = path.AdaptationMessage ?? (path.Version > 1 && path.GeneratedFromAttemptId.HasValue
            ? "Lộ trình đã được điều chỉnh theo kết quả học tập mới nhất. Các buổi ôn và mục tiêu tuần đã được phân bổ lại theo Digital Twin." : null);

        return new DetailedLearningPathDto
        {
            LearningPathId = path.LearningPathId.ToString(), StudentId = studentId.ToString(), SubjectId = subject.SubjectId.ToString(),
            SubjectName = subject.SubjectName, Strategy = path.Strategy.ToString(), Version = path.Version, Status = path.Status.ToString(),
            GeneratedAt = path.GeneratedAt, CurrentOverallMastery = currentOverall, TargetMastery = preference.TargetMastery,
            EstimatedWeeks = preference.TargetWeeks, MinutesPerDay = preference.MinutesPerDay, DaysPerWeek = preference.DaysPerWeek,
            ProgressPercentage = progress, GoalType = preference.GoalType, TotalScheduledMinutes = allSessions.Sum(s => s.EstimatedMinutes),
            CapacityMinutes = preference.TargetWeeks * preference.DaysPerWeek * preference.MinutesPerDay,
            GenerationStatus = path.GenerationStatus, RecommendationRationale = rationale, AdaptationMessage = adaptation,
            NextSession = allSessions.FirstOrDefault(s => s.Status is "NotStarted" or "NeedsReview" or "InProgress"), Phases = phases
        };
    }

    private async Task<List<LearningPathPhaseDto>> BuildScheduleAsync(Guid centerId, Guid subjectId, Guid pathId,
        IReadOnlyList<LearningPathItemDetailDto> items, StudentLearningPathPreference preference, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return new();
        var topicIds = items.Select(i => ulong.Parse(i.TopicNodeId)).ToList();
        var questions = await _dbContext.Questions.AsNoTracking()
            .Where(q => q.CenterId == centerId && q.SubjectId == subjectId && topicIds.Contains(q.PrimaryTopicNodeId) && !q.IsDeleted)
            .OrderBy(q => q.Difficulty).ThenBy(q => q.QuestionId)
            .Select(q => new { q.QuestionId, q.PrimaryTopicNodeId }).ToListAsync(cancellationToken);
        var questionsByTopic = questions.GroupBy(q => q.PrimaryTopicNodeId).ToDictionary(g => g.Key, g => g.Select(q => q.QuestionId.ToString()).ToList());

        var weeks = Math.Clamp(preference.TargetWeeks, 1, 52);
        var days = Math.Clamp(preference.DaysPerWeek, 1, 7);
        var phaseCount = Math.Clamp((int)Math.Ceiling(weeks / 4m), 1, 6);
        var weeksPerPhase = (int)Math.Ceiling(weeks / (decimal)phaseCount);
        var phases = new List<LearningPathPhaseDto>();
        for (var phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            var startWeek = phaseIndex * weeksPerPhase + 1;
            var endWeek = Math.Min(weeks, startWeek + weeksPerPhase - 1);
            if (startWeek > weeks) break;
            var phaseWeeks = new List<LearningPathWeekDto>();
            for (var week = startWeek; week <= endWeek; week++)
            {
                var topic = items[Math.Min(items.Count - 1, (week - 1) * items.Count / weeks)];
                var questionIds = questionsByTopic.GetValueOrDefault(ulong.Parse(topic.TopicNodeId)) ?? new();
                var weekTarget = Math.Round(topic.CurrentMastery + (topic.TargetMastery - topic.CurrentMastery) * week / weeks, 1);
                var sessions = new List<LearningPathSessionDto>();
                for (var day = 1; day <= days; day++)
                {
                    var type = GetSessionType(day, days, topic.CurrentMastery);
                    var selectedQuestions = questionIds.Count == 0 ? new List<string>() : questionIds.Skip(((week - 1) * days + day - 1) * 8 % questionIds.Count).Take(8).ToList();
                    var online = BuildOnlineActivities(type, topic.TopicName, selectedQuestions.Count);
                    if (selectedQuestions.Count == 0 && type is "GuidedPractice" or "AdaptivePractice" or "Quiz" or "MockTest")
                        online.Add("Chưa có bộ bài phù hợp; học lý thuyết và tự kiểm tra bằng ví dụ trong bài học.");
                    sessions.Add(new LearningPathSessionDto
                    {
                        SessionId = $"{pathId:D}-w{week:D2}-s{day:D2}", Title = BuildSessionTitle(type, topic.TopicName), Type = type,
                        TopicNodeId = topic.TopicNodeId, TopicName = topic.TopicName, Objective = BuildObjective(type, topic.TopicName),
                        EstimatedMinutes = preference.MinutesPerDay, OnlineActivities = online, OfflineActivities = BuildOfflineActivities(type, topic.TopicName),
                        RecommendedQuestionIds = selectedQuestions, RequiredCorrectRate = type is "Quiz" or "MockTest" ? 0.75m : 0.70m,
                        TargetMastery = weekTarget, CompletionCriteria = BuildCompletionCriteria(type, selectedQuestions.Count, weekTarget), Status = topic.Status
                    });
                }
                phaseWeeks.Add(new LearningPathWeekDto
                {
                    WeekNumber = week, Title = $"Tuần {week} · {topic.TopicName}", Objective = $"Nâng mức thành thạo {topic.TopicName} từ {topic.CurrentMastery:0.#}% tới khoảng {weekTarget:0.#}%.",
                    ProgressPercentage = Math.Round((decimal)sessions.Count(s => s.Status == "Completed") / sessions.Count * 100m, 1), Sessions = sessions,
                    Checkpoint = new LearningPathCheckpointDto
                    {
                        Type = week % 4 == 0 || week == weeks ? "Monthly" : "Weekly", CurrentMastery = topic.CurrentMastery,
                        TargetMastery = weekTarget, RequiredCorrectRate = 0.75m, CompletedTasks = sessions.Count(s => s.Status == "Completed"),
                        WeakTopics = new List<string> { topic.TopicName },
                        Recommendation = "Nếu dưới 75% câu đúng, chuyển task sang NeedsReview và thêm buổi củng cố; nếu đạt sớm, giảm lặp lại và tăng độ khó."
                    }
                });
            }
            var phaseName = phaseIndex == 0 ? "Củng cố nền tảng" : phaseIndex == phaseCount - 1 ? "Tổng hợp và kiểm tra" : "Khắc phục chuyên đề yếu";
            phases.Add(new LearningPathPhaseDto
            {
                PhaseNumber = phaseIndex + 1, PhaseName = $"Giai đoạn {phaseIndex + 1}: {phaseName}",
                Timeframe = startWeek == endWeek ? $"Tuần {startWeek}" : $"Tuần {startWeek} - {endWeek}",
                Description = $"Học theo thứ tự ưu tiên của Recommendation Engine trong {endWeek - startWeek + 1} tuần.",
                ProgressPercentage = Math.Round(phaseWeeks.Average(w => w.ProgressPercentage), 1), Weeks = phaseWeeks
            });
        }
        return phases;
    }

    private async Task EnrichPlanAsync(DetailedLearningPathDto plan, CancellationToken cancellationToken)
    {
        if (_planEnricher is null) return;
        var sessions = plan.Phases.SelectMany(p => p.Weeks).SelectMany(w => w.Sessions).ToList();
        try
        {
            var result = await _planEnricher.EnrichAsync(new LearningPathEnrichmentRequest(
                plan.SubjectName, plan.GoalType, plan.EstimatedWeeks, plan.MinutesPerDay, plan.DaysPerWeek,
                sessions.Select(s => new LearningPathEnrichmentSessionInput(s.SessionId, s.TopicName, s.Type, s.Objective, s.OnlineActivities, s.OfflineActivities)).ToList(),
                plan.RecommendationRationale), cancellationToken);
            if (result is null) return;
            var byId = result.Sessions.GroupBy(s => s.SessionId).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
            foreach (var session in sessions)
            {
                if (!byId.TryGetValue(session.SessionId, out var enriched)) continue;
                if (!string.IsNullOrWhiteSpace(enriched.Objective)) session.Objective = enriched.Objective.Trim();
                if (enriched.OnlineActivities.Count > 0) session.OnlineActivities = enriched.OnlineActivities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Take(5).ToList();
                if (enriched.OfflineActivities.Count > 0) session.OfflineActivities = enriched.OfflineActivities.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Take(5).ToList();
            }
            if (!string.IsNullOrWhiteSpace(result.Summary)) plan.RecommendationRationale = result.Summary.Trim();
        }
        catch
        {
            // Deterministic schedule is the required safe fallback when the provider is unavailable or invalid.
        }
    }

    private StudentLearningPathPreference UpsertPreference(StudentLearningPathPreference? p, Guid centerId, Guid studentId,
        GenerateLearningPathRequest r, List<ulong> weakIds, List<ulong> focusIds, DateTime now)
    {
        if (p is null)
        {
            p = new StudentLearningPathPreference { CenterId = centerId, StudentId = studentId, SubjectId = r.SubjectId, CreatedAt = now };
            _dbContext.StudentLearningPathPreferences.Add(p);
        }
        p.SelfAssessedLevel = r.SelfAssessedLevel; p.WeakTopicNodeIds = JsonSerializer.SerializeToDocument(weakIds);
        p.FocusTopicNodeIds = JsonSerializer.SerializeToDocument(focusIds); p.GoalType = r.GoalType; p.TargetMastery = r.TargetMastery;
        p.TargetWeeks = r.TargetWeeks; p.MinutesPerDay = r.MinutesPerDay; p.DaysPerWeek = r.DaysPerWeek; p.Pace = r.Pace;
        p.PreferredMode = r.PreferredMode; p.Note = r.Note; p.UpdatedAt = now; p.UpdatedBy = studentId;
        return p;
    }

    private static bool PreferenceMatches(StudentLearningPathPreference p, GenerateLearningPathRequest r, List<ulong> weak, List<ulong> focus) =>
        p.SelfAssessedLevel == r.SelfAssessedLevel && p.GoalType == r.GoalType && p.TargetMastery == r.TargetMastery &&
        p.TargetWeeks == r.TargetWeeks && p.MinutesPerDay == r.MinutesPerDay && p.DaysPerWeek == r.DaysPerWeek && p.Pace == r.Pace &&
        p.PreferredMode == r.PreferredMode && p.Note == r.Note && ReadIds(p.WeakTopicNodeIds).SequenceEqual(weak) && ReadIds(p.FocusTopicNodeIds).SequenceEqual(focus);
    private static List<ulong> ReadIds(JsonDocument doc) => JsonSerializer.Deserialize<List<ulong>>(doc.RootElement.GetRawText()) ?? new();
    private (Guid, Guid) ResolveStudent()
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is not { } centerId || _tenantContext.UserId is not { } studentId)
            throw new InvalidOperationException("Tenant context is not resolved.");
        return (centerId, studentId);
    }
    private static void ValidateRequest(GenerateLearningPathRequest r)
    {
        if (r.SubjectId == Guid.Empty || r.TargetWeeks is < 1 or > 52 || r.MinutesPerDay is < 10 or > 180 || r.DaysPerWeek is < 1 or > 7 || r.TargetMastery is < 40 or > 100)
            throw new ArgumentException("Thông tin mục tiêu hoặc thời gian học không hợp lệ.");
    }
    private static decimal CalculateRealisticTarget(decimal current, decimal requested) => Math.Round(Math.Min(requested, current < 30m ? 60m : Math.Min(90m, current + 25m)), 1);
    private static string MapItemStatus(string status) => status switch { "Current" => "InProgress", "Completed" => "Completed", "Skipped" => "Skipped", _ => "NotStarted" };
    private static string GetSessionType(int day, int days, decimal mastery) => day == days ? "Quiz" : day switch { 1 => mastery < 40 ? "LearnTheory" : "Review", 2 => "GuidedPractice", 3 => "AdaptivePractice", 4 => "Review", _ => "Reflection" };
    private static string BuildSessionTitle(string type, string topic) => type switch { "LearnTheory" => $"Nắm nền tảng {topic}", "GuidedPractice" => $"Luyện có hướng dẫn: {topic}", "AdaptivePractice" => $"Luyện thích ứng: {topic}", "Quiz" => $"Mini quiz: {topic}", "Review" => $"Ôn và sửa lỗi: {topic}", _ => $"Tự phản tư: {topic}" };
    private static string BuildObjective(string type, string topic) => type switch { "LearnTheory" => $"Hiểu khái niệm và quy tắc cốt lõi của {topic}.", "Quiz" => $"Kiểm tra khả năng vận dụng {topic} độc lập.", _ => $"Củng cố kỹ năng và nhận diện lỗi thường gặp trong {topic}." };
    private static List<string> BuildOnlineActivities(string type, string topic, int count)
    {
        var result = new List<string>();
        if (type is "LearnTheory" or "Review") result.Add($"Đọc bài và xem lại ví dụ về {topic} trên EduTwin.");
        if (count > 0) result.Add($"Làm {count} câu thật từ Question Bank theo đúng chủ đề.");
        if (type == "AdaptivePractice") result.Add("Xem lại ba lỗi gần nhất và đối chiếu phản hồi AI.");
        if (type == "Quiz") result.Add("Làm mini quiz trong thời gian của buổi học, không xem lời giải trước.");
        return result;
    }
    private static List<string> BuildOfflineActivities(string type, string topic) => type switch
    {
        "LearnTheory" => new() { $"Viết một trang tóm tắt công thức/khái niệm {topic} vào vở.", "Tự đặt hai ví dụ và giải thích bằng lời." },
        "Quiz" => new() { "Ghi các lỗi sai vào sổ lỗi.", "Giải lại hai câu sai mà không xem đáp án." },
        "Reflection" => new() { "Viết ba điều đã hiểu và một điều còn vướng.", "Chuẩn bị câu hỏi để hỏi giáo viên nếu vẫn chưa rõ." },
        _ => new() { "Giải lại ít nhất hai bài bằng giấy.", "Đánh dấu bước dễ nhầm và cách tự kiểm tra." }
    };
    private static List<string> BuildCompletionCriteria(string type, int count, decimal target) => new()
    {
        type == "LearnTheory" ? "Hoàn thành phần lý thuyết và ghi chú." : count > 0 ? $"Hoàn thành {count} câu được giao." : "Hoàn thành hoạt động thay thế không cần Question Bank.",
        type is "Quiz" or "AdaptivePractice" ? "Đạt tối thiểu 75% câu đúng; nếu chưa đạt chuyển sang NeedsReview." : "Tự giải thích lại được nội dung chính.",
        $"Hướng tới mastery {target:0.#}% tại checkpoint."
    };
    private static string BuildRationale(string subject, StudentLearningPathPreference p, IReadOnlyList<LearningPathItemDetailDto> items)
    {
        var weak = items.Count == 0 ? "các kiến thức nền" : string.Join(", ", items.Take(2).Select(i => $"{i.TopicName} ({i.CurrentMastery:0.#}%)"));
        return $"Bạn đang cần ưu tiên {weak}. EduTwin xếp kiến thức nền trước theo Knowledge Graph và Recommendation Engine. " +
               $"Kế hoạch phục vụ mục tiêu {GoalLabel(p.GoalType)} môn {subject} trong {p.TargetWeeks} tuần, với {p.MinutesPerDay} phút/ngày và {p.DaysPerWeek} ngày/tuần.";
    }
    private static string GoalLabel(string goal) => goal switch { "ExamPrep" => "ôn thi", "Foundation" => "củng cố nền tảng", "KeepUp" => "theo kịp chương trình", "ImproveGrade" => "cải thiện điểm số", "Advanced" => "học nâng cao", _ => "học tập" };
}
