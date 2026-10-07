using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public sealed class AIReasoningAnalysisBuilder : IAIReasoningAnalysisBuilder
{
    public ReasoningAnalysis Build(
        Guid centerId,
        ulong attemptId,
        AnalyzeReasoningResponse response,
        DateTime utcNow,
        bool? preliminaryIsCorrect = null,
        string language = "vi")
    {
        if (centerId == Guid.Empty)
        {
            throw new ArgumentException("Center ID is required.", nameof(centerId));
        }

        if (attemptId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptId));
        }

        ArgumentNullException.ThrowIfNull(response);

        // A fallacious derivation can accidentally produce the right number.
        // Preserve observations; the AI never writes a final grade.
        var disagrees = (preliminaryIsCorrect == true && response.AnswerAssessment == "Incorrect")
            || (preliminaryIsCorrect == false && response.AnswerAssessment == "Correct");
        var needsReview = preliminaryIsCorrect is null || disagrees
            || response.AnswerAssessment == "Uncertain" || response.ReasoningVerdict == "Uncertain"
            || response.Confidence < 80
            || (preliminaryIsCorrect == true && response.ReasoningVerdict == "Invalid");

        return new ReasoningAnalysis
        {
            CenterId = centerId,
            AttemptId = attemptId,
            SchemaVersion = response.SchemaVersion,
            MethodDetected = response.MethodDetected,
            ReasoningQuality = response.ReasoningQuality,
            ErrorType = response.ErrorType,
            Misconception = response.Misconception,
            MissingSteps = JsonSerializer.SerializeToDocument(
                response.MissingSteps),
            RootCauseNodeIds = JsonSerializer.SerializeToDocument(
                response.RootCauseNodeIds),
            AnalysisConfidence = response.Confidence,
            Feedback = response.Feedback,
            FeedbackOrigin = "Gemini",
            AnswerAssessment = response.AnswerAssessment,
            ReasoningVerdict = response.ReasoningVerdict,
            SolutionType = response.SolutionType,
            AiSolution = response.AiSolution,
            IsFallback = false,
            NeedsTeacherReview = needsReview,
            Provider = AnalysisProvider.Gemini,
            ModelName = null,
            OverrideReasoningQuality = null,
            OverrideErrorType = null,
            OverrideFeedback = null,
            OverrideIsCorrect = null,
            OverrideReason = null,
            OverriddenByUserId = null,
            OverriddenAt = null,
            OverrideVersion = 0,
            CreatedAt = utcNow,
            CreatedBy = null,
            UpdatedAt = utcNow
        };
    }
}
