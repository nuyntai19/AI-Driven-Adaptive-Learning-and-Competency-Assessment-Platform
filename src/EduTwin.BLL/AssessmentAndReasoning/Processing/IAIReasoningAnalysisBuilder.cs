using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public interface IAIReasoningAnalysisBuilder
{
    ReasoningAnalysis Build(
        Guid centerId,
        ulong attemptId,
        AnalyzeReasoningResponse response,
        DateTime utcNow);
}
