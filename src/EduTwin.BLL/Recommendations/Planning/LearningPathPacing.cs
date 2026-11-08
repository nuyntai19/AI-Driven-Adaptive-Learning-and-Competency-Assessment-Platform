using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>
/// Deterministic sizing rules shared by the planner and the daily agenda.
/// Weakness decides how much study a skill gets; the daily time budget decides how much of it is done today.
/// </summary>
public static class LearningPathPacing
{
    /// <summary>Questions in the end-of-route review of a self-assessed part.</summary>
    public const uint ConfirmationSize = 3;
    public const uint MinDailyMinutes = 5;
    /// <summary>A standing plan below this cannot fit one practice set a day; a single busy day may still use MinDailyMinutes.</summary>
    public const uint MinGoalDailyMinutes = 10;
    public const uint MaxDailyMinutes = 300;
    public const uint MinutesPerQuestion = 2;
    public const uint MinPracticeQuestions = 3;
    public const uint MaxPracticeQuestions = 20;
    public const uint IndependentCheckQuestions = 5;
    /// <summary>Every practice set has the same short size so results compare across runs and feedback comes early.</summary>
    public const uint SetSize = 6;
    public const uint ProbeSize = 4;
    /// <summary>Consecutive passing sets needed before the independent check is recommended.</summary>
    public const uint ReadyStreak = 2;
    public const decimal PassRate = .8m;
    public const decimal RepairRate = .5m;
    public const decimal ReviewPassRate = .6m;
    /// <summary>Upper bound of sets per skill in one route before the system asks for a teacher instead of more drilling.</summary>
    public const uint MaxSetsPerSkill = 8;
    /// <summary>After this many sets of one skill today, another skill is preferred (interleaving).</summary>
    public const uint MaxSetsPerSkillPerDay = 3;

    /// <summary>0 = vững, 1 = rất yếu. Uses the latest answer to each distinct question, never repeated failures.</summary>
    public static decimal Weakness(string state, LearningSkillInsightDto? insight)
    {
        if (state == "Confirmed") return .1m;
        // A correct answer reached with a hint counts as half a correct answer.
        decimal? rate = insight is { QuestionCount: > 0 } ? (insight.WrongCount + .5m * insight.HintedCorrectCount) / insight.QuestionCount : null;
        var value = rate ?? (state == "Developing" ? .6m : .5m);
        if (state == "Developing") value = Math.Max(value, .5m);
        // A small sample cannot prove strength: one or two correct answers stay at "cần củng cố".
        if (insight is { QuestionCount: < 3 } && value < .4m) value = .4m;
        return Math.Clamp(value, .1m, 1m);
    }

    public static string Level(decimal weakness) => weakness >= .6m ? "Weak" : weakness >= .3m ? "Developing" : "Strong";

    public static string LevelLabel(decimal weakness) => Level(weakness) switch
    {
        "Weak" => "Phần yếu",
        "Developing" => "Cần củng cố",
        _ => "Khá vững"
    };

    /// <summary>Expected number of 6-question sets before a skill is ready for its check. Only an estimate: the loop decides.</summary>
    public static uint EstimatedSetsFor(decimal weakness) => Level(weakness) switch { "Weak" => 4u, "Developing" => 3u, _ => 2u };

    /// <summary>Extra sets when the target score sits above the skill's current mastery; unknown mastery counts as 50%.</summary>
    public static uint ExtraSetsForTargetGap(decimal? targetScore, decimal? masteryPercent)
    {
        if (targetScore is null) return 0;
        var gap = Math.Max(0m, targetScore.Value * 10m - (masteryPercent ?? 50m));
        return (uint)Math.Min(3, (int)Math.Ceiling(gap / 20m));
    }

    /// <summary>Starting difficulty (1-5) of the first set: weak skills start easy and climb.</summary>
    public static byte StartDifficulty(decimal weakness) => Level(weakness) switch { "Weak" => 1, "Developing" => 2, _ => 3 };

    /// <summary>Accuracy of a set: correct answers, hinted ones counted half, over graded answers.</summary>
    public static decimal? Accuracy(int graded, int correct, int hintedCorrect)
        => graded == 0 ? null : Math.Round((correct - .5m * hintedCorrect) / graded, 3);

    public static uint ClampPracticeSize(int value, int pool)
        => (uint)Math.Max(0, Math.Min(pool, Math.Clamp(value, (int)MinPracticeQuestions, (int)MaxPracticeQuestions)));

    public static uint ClampMinutes(uint minutes) => Math.Clamp(minutes, MinDailyMinutes, MaxDailyMinutes);

    /// <summary>Average teacher-set seconds per question in a pool (0 when the
    /// pool carries no estimates). Plans must divide the daily budget by this
    /// pace — 90-second questions give ~3 questions in 5 minutes, not 1.</summary>
    public static double AverageSecondsPerQuestion(IEnumerable<uint> estimatedSeconds)
    {
        var values = estimatedSeconds.Where(s => s > 0).Select(s => (double)s).ToList();
        return values.Count == 0 ? 0 : values.Average();
    }

    /// <summary>Whole minutes a set of questions is expected to take at the
    /// pool's own pace; the flat default applies when the pace is unknown.</summary>
    public static uint EstimatedMinutes(uint count, double averageSecondsPerQuestion)
        => averageSecondsPerQuestion <= 0
            ? Math.Max(1u, count) * MinutesPerQuestion
            : Math.Max(1u, (uint)Math.Ceiling(count * averageSecondsPerQuestion / 60d));

    public static string FormatMinutes(uint minutes)
    {
        var h = minutes / 60; var m = minutes % 60;
        return h == 0 ? $"{m} phút" : m == 0 ? $"{h} giờ" : $"{h} giờ {m} phút";
    }
}
