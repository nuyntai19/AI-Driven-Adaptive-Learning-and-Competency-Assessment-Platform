using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin.Orchestration;

public sealed class TwinCompletionOrchestrator : ITwinCompletionOrchestrator
{
    private readonly EduTwinDbContext _dbContext;
    private readonly IEvidenceGate _evidenceGate;
    private readonly IEvidenceAssessmentFactory _evidenceAssessmentFactory;
    private readonly IBehaviorTwinUpdater _behaviorTwinUpdater;
    private readonly IKnowledgeTwinUpdater _knowledgeTwinUpdater;
    private readonly ITwinUpdateHistoryWriter _historyWriter;
    private readonly IStudentGoalRiskUpdater _goalRiskUpdater;
    private readonly IStudentTwinUpdater _studentTwinUpdater;

    public TwinCompletionOrchestrator(
        EduTwinDbContext dbContext,
        IEvidenceGate evidenceGate,
        IEvidenceAssessmentFactory evidenceAssessmentFactory,
        IBehaviorTwinUpdater behaviorTwinUpdater,
        IKnowledgeTwinUpdater knowledgeTwinUpdater,
        ITwinUpdateHistoryWriter historyWriter,
        IStudentGoalRiskUpdater goalRiskUpdater,
        IStudentTwinUpdater studentTwinUpdater)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _evidenceGate = evidenceGate ?? throw new ArgumentNullException(nameof(evidenceGate));
        _evidenceAssessmentFactory = evidenceAssessmentFactory ?? throw new ArgumentNullException(nameof(evidenceAssessmentFactory));
        _behaviorTwinUpdater = behaviorTwinUpdater ?? throw new ArgumentNullException(nameof(behaviorTwinUpdater));
        _knowledgeTwinUpdater = knowledgeTwinUpdater ?? throw new ArgumentNullException(nameof(knowledgeTwinUpdater));
        _historyWriter = historyWriter ?? throw new ArgumentNullException(nameof(historyWriter));
        _goalRiskUpdater = goalRiskUpdater ?? throw new ArgumentNullException(nameof(goalRiskUpdater));
        _studentTwinUpdater = studentTwinUpdater ?? throw new ArgumentNullException(nameof(studentTwinUpdater));
    }

    public async Task<TwinCompletionResult> CompleteAsync(
        Attempt attempt,
        Question question,
        ReasoningAnalysis analysis,
        TwinEventSource eventSource,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(analysis);

        // 1. Evaluate Evidence Gate
        var gateSource = eventSource == TwinEventSource.RuleFallback
            ? EvidenceSourceType.RuleFallback
            : (eventSource == TwinEventSource.TeacherOverride ? EvidenceSourceType.TeacherOverride : EvidenceSourceType.AI);

        var gateInput = new EvidenceGateInput(
            SourceType: gateSource,
            StructuralValidationPassed: true,
            SemanticValidationPassed: true,
            HasContradiction: false,
            HasAnomaly: false,
            HasRequiredEvidence: analysis.ReasoningQuality.HasValue,
            EffectiveIsCorrect: attempt.IsCorrect,
            AnalysisConfidence: analysis.AnalysisConfidence,
            AnalysisOverrideVersion: analysis.OverrideVersion);

        var decision = _evidenceGate.Evaluate(gateInput);
        analysis.NeedsTeacherReview = decision.RequiresTeacherReview;

        // 2. Persist ReasoningAnalysis and EvidenceAssessment
        var evidence = _evidenceAssessmentFactory.Create(
            attempt,
            analysis,
            supersedes: null,
            decision,
            utcNow,
            createdBy: null);

        _dbContext.ReasoningAnalyses.Add(analysis);
        _dbContext.EvidenceAssessments.Add(evidence);

        // 3. Update Behavior Twin from observed telemetry
        var behaviorTwin = await _behaviorTwinUpdater.UpdateAsync(
            attempt,
            question.SubjectId,
            utcNow,
            cancellationToken);

        // 4. Update Knowledge Twin via MasteryCalculator
        var knowledgeResult = await _knowledgeTwinUpdater.UpdateAsync(
            attempt,
            question,
            analysis,
            evidence,
            behaviorTwin.ConfidenceCalibration,
            utcNow,
            cancellationToken);

        // 5. Append Twin Update History
        var history = await _historyWriter.WriteAsync(
            attempt.CenterId,
            attempt.StudentId,
            question.SubjectId,
            question.PrimaryTopicNodeId,
            attempt.AttemptId,
            analysis.AnalysisId,
            eventSource,
            knowledgeResult.Calculation,
            utcNow,
            createdBy: null,
            cancellationToken);

        history.Analysis = analysis;
        history.Attempt = attempt;

        // 6. Update Student Subject Goal (Predicted & Risk Scores)
        var goal = await _goalRiskUpdater.UpdateAsync(
            attempt.CenterId,
            attempt.StudentId,
            question.SubjectId,
            utcNow,
            cancellationToken);

        // 7. Update Student Twin Overall Mastery
        var studentTwin = await _studentTwinUpdater.UpdateAsync(
            attempt.CenterId,
            attempt.StudentId,
            utcNow,
            cancellationToken);

        // 8. Update Student Assignment Progress if attempt belongs to an assignment
        if (attempt.AssignmentId.HasValue)
        {
            var progress = await _dbContext.StudentAssignmentProgresses
                .SingleOrDefaultAsync(
                    p => p.CenterId == attempt.CenterId
                        && p.AssignmentId == attempt.AssignmentId.Value
                        && p.StudentId == attempt.StudentId
                        && !p.IsDeleted,
                    cancellationToken);

            if (progress is not null)
            {
                var dbQuestionIds = await _dbContext.Attempts
                    .Where(a => a.CenterId == attempt.CenterId
                        && a.AssignmentId == attempt.AssignmentId.Value
                        && a.StudentId == attempt.StudentId
                        && (a.Status == AttemptStatus.Completed
                            || a.Status == AttemptStatus.NeedsTeacherReview
                            || a.AttemptId == attempt.AttemptId))
                    .Select(a => a.QuestionId)
                    .ToListAsync(cancellationToken);

                var localQuestionIds = _dbContext.Attempts.Local
                    .Where(a => a.CenterId == attempt.CenterId
                        && a.AssignmentId == attempt.AssignmentId.Value
                        && a.StudentId == attempt.StudentId
                        && (a.Status == AttemptStatus.Completed
                            || a.Status == AttemptStatus.NeedsTeacherReview
                            || a.AttemptId == attempt.AttemptId))
                    .Select(a => a.QuestionId);

                var answeredQuestionCount = dbQuestionIds
                    .Concat(localQuestionIds)
                    .Append(attempt.QuestionId)
                    .Distinct()
                    .Count();

                progress.CompletedQuestionCount = (uint)answeredQuestionCount;
                if (progress.CompletedQuestionCount >= progress.TotalQuestionCount && progress.TotalQuestionCount > 0)
                {
                    progress.Status = ProgressStatus.Completed;
                    progress.CompletedAt ??= utcNow;
                }
                else if (progress.Status == ProgressStatus.NotStarted)
                {
                    progress.Status = ProgressStatus.InProgress;
                    progress.StartedAt ??= utcNow;
                }
                progress.UpdatedAt = utcNow;
            }
        }

        // 9. Update Attempt Status
        attempt.Status = decision.RequiresTeacherReview
            ? AttemptStatus.NeedsTeacherReview
            : AttemptStatus.Completed;
        attempt.UpdatedAt = utcNow;

        return new TwinCompletionResult(
            evidence,
            knowledgeResult.Twin,
            behaviorTwin,
            history,
            goal,
            studentTwin);
    }
}
