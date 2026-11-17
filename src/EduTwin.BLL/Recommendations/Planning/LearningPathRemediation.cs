using System.Text.RegularExpressions;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>Subskill repair uses authored objectives and graded submissions, never an AI guess about a distractor.</summary>
public static class LearningPathRemediation
{
    public const uint MaxRounds = 6;
    public static string? CheckBlockReason(PathProposalDto plan, ProposedMissionDto check)
    {
        // Placement/diagnostic and scheduled exams retain their separate policies.
        if (plan.IsPlacement || check.Purpose != "IndependentCheck" || check.IsProbe || check.IsMilestoneExam || check.IsExternalPrerequisiteAssessment) return null;
        var milestone = plan.Milestones.FirstOrDefault(m => m.Missions.Any(mission => mission.MissionId == check.MissionId));
        if (milestone == null || milestone.ReadyForCheck || !milestone.Missions.Any(mission => mission.SkillId == check.SkillId &&
            mission.Purpose == "Practice" && mission.RemediationRound > 0)) return null;
        return "Nhánh ôn phần yếu và phần luyện tổng hợp chưa hoàn tất. Hoàn thành các vòng ôn rồi đạt các bộ luyện tổng hợp trước khi tự kiểm tra; thiếu câu phần yếu thì cần giáo viên bổ sung, không tự coi là đã vững.";
    }

    public static string ProgressStatus(string? decisionCode, bool missingQuestions) => decisionCode switch {
        "FOCUS_REPAIR_RETURN" => "ReturnedToMain",
        "FOCUS_REPAIR_NEEDS_QUESTIONS" => "NeedsQuestions",
        "FOCUS_REPAIR_SUPPORT" => missingQuestions ? "NeedsQuestions" : "NeedsSupport",
        "FOCUS_REPAIR_BRANCH" => "NeedsPractice",
        _ => "NotConfirmed"
    };
    // Stored set numbers remain the total practice count for limits/history.
    // Display numbers count only the main route; repair rounds have their own labels.
    public static Dictionary<string, uint> MainSetNumbers(IEnumerable<ProposedMissionDto> missions) => missions
        .Where(mission => mission.Purpose == "Practice" && mission.RemediationRound == 0)
        .GroupBy(mission => mission.SkillId)
        .SelectMany(group => group.Select((mission, index) => (mission.MissionId, Number: (uint)index + 1)))
        .ToDictionary(item => item.MissionId, item => item.Number);

    public static string DisplayTitle(ProposedMissionDto mission, IReadOnlyDictionary<string, uint> mainNumbers)
    {
        if (mission.RemediationRound > 0 && mission.RemediationFocuses.Count > 0)
            return mission.Purpose == "Practice"
                ? $"Rèn lại bằng bài tập: {string.Join(" + ", mission.RemediationFocuses)} · vòng {mission.RemediationRound}"
                : mission.Purpose == "Study" ? $"Ôn kiến thức: {string.Join(" + ", mission.RemediationFocuses)}" : mission.Title;
        return mission.Purpose == "Practice" && mainNumbers.TryGetValue(mission.MissionId, out var number)
            ? Regex.Replace(mission.Title, @"· bộ \d+$", $"· bộ {number}") : mission.Title;
    }

    public static string? OriginLabel(ProposedMissionDto mission, IReadOnlyDictionary<string, ProposedMissionDto> definitions,
        IReadOnlyDictionary<string, uint> mainNumbers)
    {
        if (mission.RemediationSourceMissionId == null || !definitions.TryGetValue(mission.RemediationSourceMissionId, out var source)) return null;
        return source.RemediationRound > 0 ? $"Tiếp nối bài tập phần yếu vòng {source.RemediationRound}"
            : mainNumbers.TryGetValue(source.MissionId, out var number) ? $"Nhánh bổ trợ sau bộ luyện chính {number}"
            : $"Nhánh bổ trợ sau: {source.Title}";
    }

    /// <summary>Keep at least 80% focused practice even when the bank or daily budget is small.</summary>
    public static (int Target, int Contrast) Composition(int targetAvailable, int contrastAvailable, int requested)
    {
        requested = Math.Max(0, requested);
        if (targetAvailable <= 0 || requested == 0) return (0, 0);
        var contrast = Math.Min(contrastAvailable, requested / 5);
        var target = Math.Min(targetAvailable, requested - contrast);
        contrast = Math.Min(contrast, target / 4);
        return (target, contrast);
    }
    private static readonly (string English, string Vietnamese)[] Tenses = [
        ("present perfect continuous", "Hiện tại hoàn thành tiếp diễn"), ("past perfect continuous", "Quá khứ hoàn thành tiếp diễn"),
        ("future perfect continuous", "Tương lai hoàn thành tiếp diễn"), ("present continuous", "Hiện tại tiếp diễn"),
        ("past continuous", "Quá khứ tiếp diễn"), ("future continuous", "Tương lai tiếp diễn"),
        ("present perfect", "Hiện tại hoàn thành"), ("past perfect", "Quá khứ hoàn thành"), ("future perfect", "Tương lai hoàn thành"),
        ("present simple", "Hiện tại đơn"), ("past simple", "Quá khứ đơn"), ("future simple", "Tương lai đơn")
    ];

    public static List<string> Focuses(Question question)
    {
        var authored = question.GradingCriteria.LearningFocuses ?? [];
        if (authored.Count > 0) return authored.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Compatibility for existing English banks: only known tense names in the
        // authored learning objective. Never scan options, mistakes, or reference answers.
        var objective = question.ExpectedReasoning ?? "";
        var result = new List<string>();
        foreach (var (english, vietnamese) in Tenses)
        {
            if (!Regex.IsMatch(objective, $@"\b{Regex.Escape(english)}\b", RegexOptions.IgnoreCase)) continue;
            result.Add(vietnamese);
            objective = Regex.Replace(objective, $@"\b{Regex.Escape(english)}\b", "", RegexOptions.IgnoreCase);
        }
        return result;
    }

    private static string Normalize(string label)
    {
        var value = Regex.Replace(label.Trim(), @"\s+", " ");
        return Tenses.FirstOrDefault(tense => string.Equals(tense.English, value, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tense.Vietnamese, value, StringComparison.OrdinalIgnoreCase)).Vietnamese ?? value;
    }

    public sealed record Observation(ulong QuestionId, List<string> Focuses, bool Correct, bool Hinted);
    public static List<string> WeakFocuses(IEnumerable<Observation> observations) => observations
        .SelectMany(answer => answer.Focuses.Select(focus => (Focus: focus, Answer: answer)))
        .GroupBy(x => x.Focus, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Any(x => !x.Answer.Correct) &&
            group.Average(x => x.Answer.Correct ? x.Answer.Hinted ? .5m : 1m : 0m) < LearningPathPacing.PassRate)
        .OrderByDescending(group => group.Count(x => !x.Answer.Correct)).ThenBy(group => group.Key)
        .Select(group => group.Key).Take(2).ToList();

    public static bool Secure(IReadOnlyList<string> focuses, IEnumerable<Observation> observations, int minimumDistinct = 3, int? window = 5)
    {
        var answers = observations.ToList();
        return focuses.Count > 0 && focuses.All(focus => {
            var relevant = answers.Where(answer => answer.Focuses.Contains(focus, StringComparer.OrdinalIgnoreCase))
                .DistinctBy(answer => answer.QuestionId).Take(window ?? int.MaxValue).ToList();
            return relevant.Count >= minimumDistinct && relevant.Average(answer => answer.Correct ? answer.Hinted ? .5m : 1m : 0m) >= LearningPathPacing.PassRate;
        });
    }

    public static async Task<(List<Observation> Current, List<Observation> History)> ObserveAsync(EduTwinDbContext db,
        LearningPathSessionRun run, ProposedMissionDto original, List<ProposedMissionDto> missions, CancellationToken ct)
    {
        var root = original.RemediationRootMissionId ?? original.MissionId;
        var lineage = missions.Where(mission => mission.MissionId == root || mission.RemediationRootMissionId == root)
            .Select(mission => mission.MissionId).ToHashSet();
        var items = await db.IssuedItems.AsNoTracking().Include(item => item.Attempt).ThenInclude(attempt => attempt!.Question)
            .Include(item => item.ActivityRun).ThenInclude(activity => activity.SessionRun)
            .Where(item => item.CenterId == run.CenterId && item.Attempt != null && !item.Attempt.Skipped &&
                item.SolutionExposedAt == null && item.ActivityRun.SessionRun.StudentId == run.StudentId &&
                item.ActivityRun.SessionRun.Session.LearningPathId == run.Session.LearningPathId &&
                item.ActivityRun.SessionRun.Session.TopicNodeId == run.Session.TopicNodeId).ToListAsync(ct);
        items = items.Where(item => lineage.Contains(item.ActivityRun.SessionRun.SessionId.ToString())).ToList();
        var attemptIds = items.Select(item => item.AttemptId!.Value).ToList();
        var overrides = await db.ReasoningAnalyses.AsNoTracking().Where(analysis => analysis.CenterId == run.CenterId && attemptIds.Contains(analysis.AttemptId))
            .ToDictionaryAsync(analysis => analysis.AttemptId, analysis => analysis.OverrideIsCorrect, ct);
        Observation? Read(IssuedItem item)
        {
            var correct = overrides.GetValueOrDefault(item.AttemptId!.Value) ?? item.Attempt!.IsCorrect;
            // IsPostFeedback also marks ALL guided practice in the evidence pipeline;
            // it is not an indication that this student opened a hint. Match practice accuracy.
            return correct.HasValue ? new(item.QuestionId!.Value, Focuses(item.Attempt!.Question), correct.Value,
                item.ActivityRun.AssistanceRequestedCount > 0) : null;
        }
        List<Observation> Latest(IEnumerable<IssuedItem> source) => source.OrderByDescending(item => item.Attempt!.CreatedAt)
            .ThenByDescending(item => item.AttemptId).DistinctBy(item => item.QuestionId).Select(Read).OfType<Observation>().ToList();
        return (Latest(items.Where(item => item.ActivityRun.SessionRunId == run.SessionRunId)), Latest(items));
    }

    /// <summary>About 80% target items, balanced across focuses; keep one contrast item when possible. Input is already scope/reservation/cooldown filtered.</summary>
    public static List<Question> Select(List<Question> pool, IReadOnlyList<string> focuses, HashSet<ulong> used,
        HashSet<ulong> wrong, byte? difficulty, int size, Random? random = null)
    {
        random ??= Random.Shared;
        var labels = pool.ToDictionary(question => question.QuestionId, Focuses);
        var ordered = pool.OrderBy(question => used.Contains(question.QuestionId) ? wrong.Contains(question.QuestionId) ? 1 : 2 : 0)
            .ThenBy(question => difficulty.HasValue ? Math.Abs(question.Difficulty - difficulty.Value) : question.Difficulty)
            .ThenBy(_ => random.Next()).ToList();
        bool Target(Question question) => labels[question.QuestionId].Any(label => focuses.Contains(label, StringComparer.OrdinalIgnoreCase));
        var focused = ordered.Where(Target).ToList();
        var others = ordered.Where(question => !Target(question)).ToList();
        var composition = Composition(focused.Count, others.Count, size);
        var targetCount = composition.Target;
        var picked = new List<Question>();
        while (picked.Count < targetCount)
        {
            var before = picked.Count;
            foreach (var focus in focuses)
            {
                var question = focused.FirstOrDefault(candidate => !picked.Contains(candidate) && labels[candidate.QuestionId].Contains(focus, StringComparer.OrdinalIgnoreCase));
                if (question != null) picked.Add(question);
                if (picked.Count == targetCount) break;
            }
            if (before == picked.Count) break;
        }
        picked.AddRange(others.Take(composition.Contrast));
        return picked.OrderBy(question => question.Difficulty).ThenBy(_ => random.Next()).ToList();
    }
}
