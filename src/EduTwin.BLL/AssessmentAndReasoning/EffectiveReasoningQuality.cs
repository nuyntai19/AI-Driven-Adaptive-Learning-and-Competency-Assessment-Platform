using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning;

/// <summary>A rule-based answer verdict is not an assessment of reasoning.</summary>
public static class EffectiveReasoningQuality
{
    public static decimal? Original(ReasoningAnalysis? analysis) =>
        analysis is { IsFallback: false } ? analysis.ReasoningQuality : null;

    public static decimal? Resolve(ReasoningAnalysis? analysis) =>
        analysis?.OverrideReasoningQuality ?? Original(analysis);
}
