using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin.Orchestration;

public sealed record TwinCompletionResult(
    EvidenceAssessment Evidence,
    KnowledgeTwin KnowledgeTwin,
    BehaviorTwin BehaviorTwin,
    TwinUpdateHistory History,
    StudentSubjectGoal? Goal,
    StudentTwin StudentTwin);

public interface ITwinCompletionOrchestrator
{
    Task<TwinCompletionResult> CompleteAsync(
        Attempt attempt,
        Question question,
        ReasoningAnalysis analysis,
        TwinEventSource eventSource,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
