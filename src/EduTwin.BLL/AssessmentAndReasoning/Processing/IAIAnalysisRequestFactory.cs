using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public interface IAIAnalysisRequestFactory
{
    AnalyzeReasoningRequest Create(
        Attempt attempt,
        Question question,
        IReadOnlyList<KnowledgeNode> allowedKnowledgeNodes,
        IReadOnlyList<AnalyzeReasoningImagePart>? imageParts = null);
}
