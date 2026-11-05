using EduTwin.Contracts.Recommendations;
using System.Text.Json;

namespace EduTwin.BLL.Recommendations.Planning;

public static class LearningAssessmentPolicy
{
    public const int MinimumPlacementQuestions = 3;
    public const int DesiredQuestions = 5;
    public const string Version = "baseline-three-levels-1";

    /// <summary>Placement result: Strong at >= 80% (4/5, 3/3), NeedsSupport at <= 40%, Inconclusive between or under 3 answers.
    /// A single wrong multiple-choice answer no longer turns a 4/5 into "not passed".</summary>
    public static string ClassifyPlacement(int answered, int correct)
    {
        if (answered < MinimumPlacementQuestions) return "Inconclusive";
        var rate = correct / (decimal)answered;
        return rate >= .8m ? "Strong" : rate <= .4m ? "NeedsSupport" : "Inconclusive";
    }

    public static string Classify(int answered, int correct, bool mostlyMultipleChoice = false)
    {
        if (answered < MinimumPlacementQuestions) return "Inconclusive";
        if (answered >= DesiredQuestions && correct >= (mostlyMultipleChoice ? answered : (int)Math.Ceiling(answered * .8m))) return "Strong";
        return correct / (decimal)answered <= .4m ? "NeedsSupport" : "Inconclusive";
    }

    public static LearningScheduleSettings Settings(JsonDocument? json) => json == null ? new()
        : JsonSerializer.Deserialize<LearningScheduleSettings>(json.RootElement.GetRawText(), LearningPathV4Data.Json) ?? new();

    public static uint PracticeSize(string pace) => pace switch { "Gentle" => 3, "Accelerated" => 8, _ => 6 };
    public static uint DailySetsPerSkill(string pace) => pace switch { "Gentle" => 1, "Accelerated" => 4, _ => 3 };
    public static DateTime? ExpiresAt(JsonDocument? policy, DateTime startedAt) => policy?.RootElement.TryGetProperty("timeLimitMinutes", out var value) == true &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var minutes) && minutes > 0 ? startedAt.AddMinutes(minutes) : null;
}
