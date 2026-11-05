using System.Text.Json;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>Route-local self reports never change the evidence-backed capability map.</summary>
internal sealed class LearningGoalScope
{
    public const string ExternalAssessmentReason = "Nền tảng ngoài giáo trình chưa có đủ bằng chứng. Kiểm tra trực tiếp kỹ năng đích bằng 5 câu mới; kết quả chỉ xác nhận kỹ năng đích, không xác nhận năng lực của nền tảng ngoài phạm vi.";
    public LearningGoalScope() { }
    public List<string> TargetSkillIds { get; set; } = [];
    public List<string> SelfReportedPrerequisiteSkillIds { get; set; } = [];
    /// <summary>Parts left out of the placement check; reviewed briefly at the end of the route, never counted as mastered.</summary>
    public List<string> SelfAssessedSkillIds { get; set; } = [];
    /// <summary>Skills the student vouched for (self-reported prerequisites and self-assessed parts).</summary>
    public static List<string> Vouched(IEnumerable<string> selfReported, IEnumerable<string>? selfAssessed) => selfReported.Concat(selfAssessed ?? []).Distinct().ToList();

    public static LearningGoalScope Read(JsonDocument? document)
    {
        if (document == null) return new();
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? new() { TargetSkillIds = JsonSerializer.Deserialize<List<string>>(document.RootElement.GetRawText()) ?? [] }
            : JsonSerializer.Deserialize<LearningGoalScope>(document.RootElement.GetRawText(), LearningPathV4Data.Json) ?? new();
    }

    public JsonDocument Store() => SelfReportedPrerequisiteSkillIds.Count == 0 && SelfAssessedSkillIds.Count == 0
        ? JsonSerializer.SerializeToDocument(TargetSkillIds)
        : JsonSerializer.SerializeToDocument(this, LearningPathV4Data.Json);

    public static bool PrerequisitesSatisfied(SkillCapabilityStatusDto skill, CapabilityMapDto map, IReadOnlyCollection<string> selfReported)
        => skill.PrerequisiteSkillIds.All(id => selfReported.Contains(id) || map.Skills.Any(s => s.SkillId == id && s.State == "Confirmed") ||
            map.ExternalPrerequisiteMastery.GetValueOrDefault(id) >= 60m);

    public static bool NeedsExternalAssessment(SkillCapabilityStatusDto skill, CapabilityMapDto map, IReadOnlyCollection<string> selfReported)
        => skill.State != "Confirmed" && skill.PrerequisiteSkillIds.Any(id => !selfReported.Contains(id) &&
            map.Skills.All(s => s.SkillId != id) && map.ExternalPrerequisiteMastery.GetValueOrDefault(id) < 60m);

    public static bool CanAssessTargetDirectly(SkillCapabilityStatusDto skill, CapabilityMapDto map, IReadOnlyCollection<string> selfReported)
        => NeedsExternalAssessment(skill, map, selfReported) && skill.PrerequisiteSkillIds.All(id => selfReported.Contains(id) ||
            map.Skills.All(s => s.SkillId != id) || map.Skills.Any(s => s.SkillId == id && s.State == "Confirmed"));
}
