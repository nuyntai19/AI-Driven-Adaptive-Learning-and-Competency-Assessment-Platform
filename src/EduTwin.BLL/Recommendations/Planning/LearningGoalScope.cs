using System.Text.Json;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>Route-local self reports never change the evidence-backed capability map.</summary>
internal sealed class LearningGoalScope
{
    public LearningGoalScope() { }
    public List<string> TargetSkillIds { get; set; } = [];
    public List<string> SelfReportedPrerequisiteSkillIds { get; set; } = [];

    public static LearningGoalScope Read(JsonDocument? document)
    {
        if (document == null) return new();
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? new() { TargetSkillIds = JsonSerializer.Deserialize<List<string>>(document.RootElement.GetRawText()) ?? [] }
            : JsonSerializer.Deserialize<LearningGoalScope>(document.RootElement.GetRawText(), LearningPathV4Data.Json) ?? new();
    }

    public JsonDocument Store() => SelfReportedPrerequisiteSkillIds.Count == 0
        ? JsonSerializer.SerializeToDocument(TargetSkillIds)
        : JsonSerializer.SerializeToDocument(this, LearningPathV4Data.Json);

    public static bool PrerequisitesSatisfied(SkillCapabilityStatusDto skill, CapabilityMapDto map, IReadOnlyCollection<string> selfReported)
        => skill.PrerequisiteSkillIds.All(id => selfReported.Contains(id) || map.Skills.Any(s => s.SkillId == id && s.State == "Confirmed"));
}
