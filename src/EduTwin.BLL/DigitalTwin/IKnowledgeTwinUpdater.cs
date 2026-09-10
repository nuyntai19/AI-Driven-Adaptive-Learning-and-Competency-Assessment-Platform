using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin;

public sealed record KnowledgeTwinUpdateResult(
    KnowledgeTwin Twin,
    MasteryCalculationResult Calculation);

public interface IKnowledgeTwinUpdater
{
    Task<KnowledgeTwinUpdateResult> UpdateAsync(
        Attempt attempt,
        Question question,
        ReasoningAnalysis analysis,
        EvidenceAssessment evidence,
        decimal confidenceCalibration,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
