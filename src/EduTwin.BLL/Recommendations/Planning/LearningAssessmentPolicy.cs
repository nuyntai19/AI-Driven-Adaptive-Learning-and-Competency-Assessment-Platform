using EduTwin.Contracts.Recommendations;
using System.Text.Json;

namespace EduTwin.BLL.Recommendations.Planning;

public static class LearningAssessmentPolicy
{
    public const int MinimumPlacementQuestions = 3;
    public const int DesiredQuestions = 5;
    public const string Version = "baseline-three-levels-1";

    public static string Classify(int answered, int correct, bool mostlyMultipleChoice = false)
    {
        if (answered < MinimumPlacementQuestions) return "Inconclusive";
        if (answered >= DesiredQuestions && correct >= (mostlyMultipleChoice ? answered : (int)Math.Ceiling(answered * .8m))) return "Strong";
        return correct / (decimal)answered <= .4m ? "NeedsSupport" : "Inconclusive";
    }

    public static LearningScheduleSettings Settings(JsonDocument? json) => json == null ? new()
        : JsonSerializer.Deserialize<LearningScheduleSettings>(json.RootElement.GetRawText(), LearningPathV4Data.Json) ?? new();

    public static uint PracticeSize(string pace) => pace switch { "Gentle" => 3, "Accelerated" => 8, _ => 6 };
    public static DateTime? ExpiresAt(JsonDocument? policy, DateTime startedAt) => policy?.RootElement.TryGetProperty("timeLimitMinutes", out var value) == true &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var minutes) && minutes > 0 ? startedAt.AddMinutes(minutes) : null;
}
