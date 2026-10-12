namespace EduTwin.BLL.AssessmentAndReasoning.AI;

// Only application-defined codes cross the job/UI boundary; never provider bodies.
public interface IAIAnalysisFailure
{
    string ErrorCode { get; }
}

public enum AIResponseValidationRule
{
    General, Json, Shape, RequestContext, SchemaVersion, Language, PercentageRange,
    Text, Verdict, KnowledgeNodeScope, ProposalRange, Rubric, VietnameseExplanation, ReasoningConsistency
}
