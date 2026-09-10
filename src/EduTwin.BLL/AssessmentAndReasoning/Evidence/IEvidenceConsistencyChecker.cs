using System.Collections.Generic;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public interface IEvidenceConsistencyChecker
{
    EvidenceConsistencyResult Evaluate(
        Attempt attempt,
        Question question,
        ReasoningAnalysis analysis,
        IReadOnlyCollection<ulong>? allowedNodeIds = null);
}
