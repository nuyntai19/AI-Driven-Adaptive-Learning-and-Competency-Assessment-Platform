using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public interface IRuleBasedFallbackBuilder
{
    ReasoningAnalysis Build(RuleBasedFallbackInput input);
}
