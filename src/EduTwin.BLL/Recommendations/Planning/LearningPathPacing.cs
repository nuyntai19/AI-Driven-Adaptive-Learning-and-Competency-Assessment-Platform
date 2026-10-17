using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>
/// Deterministic sizing rules shared by the planner and the daily agenda.
/// Weakness decides how much study a skill gets; the daily time budget decides how much of it is done today.
/// </summary>
public static class LearningPathPacing
{
    public const uint MinDailyMinutes = 5;
    public const uint MaxDailyMinutes = 300;
    public const uint MinutesPerQuestion = 2;
    public const uint MinPracticeQuestions = 3;
    public const uint MaxPracticeQuestions = 20;
    public const uint IndependentCheckQuestions = 5;

    /// <summary>0 = vững, 1 = rất yếu. Uses the latest answer to each distinct question, never repeated failures.</summary>
    public static decimal Weakness(string state, LearningSkillInsightDto? insight)
    {
        if (state == "Confirmed") return .1m;
        decimal? rate = insight is { QuestionCount: > 0 } ? insight.WrongCount / (decimal)insight.QuestionCount : null;
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

    /// <summary>Total practice questions planned for a skill across the whole path.</summary>
    public static int PracticeQuestionsFor(decimal weakness) => (int)Math.Round(5 + 11 * weakness, MidpointRounding.AwayFromZero);

    public static uint ClampPracticeSize(int value, int pool)
        => (uint)Math.Max(0, Math.Min(pool, Math.Clamp(value, (int)MinPracticeQuestions, (int)MaxPracticeQuestions)));

    public static uint ClampMinutes(uint minutes) => Math.Clamp(minutes, MinDailyMinutes, MaxDailyMinutes);

    public static string FormatMinutes(uint minutes)
    {
        var h = minutes / 60; var m = minutes % 60;
        return h == 0 ? $"{m} phút" : m == 0 ? $"{h} giờ" : $"{h} giờ {m} phút";
    }
}
