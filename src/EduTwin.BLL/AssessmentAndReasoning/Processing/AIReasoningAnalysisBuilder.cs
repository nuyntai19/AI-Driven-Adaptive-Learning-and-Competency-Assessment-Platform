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

        var deterministicFeedback = preliminaryIsCorrect switch
        {
            true when string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase) =>
                "Đáp án của bạn đã được hệ thống chấm đúng. AI chỉ phân tích phương pháp và gợi ý lời giải tối ưu; kết quả đúng/sai không bị AI chấm lại.",
            true =>
                "Your answer was graded correct by the deterministic grader. AI only analyzes the method and suggests an improved solution; it does not re-grade correctness.",
            false when string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase) =>
                "Đáp án của bạn chưa đúng theo kết quả chấm xác định. Hãy đối chiếu lời giải đề xuất để tìm bước cần điều chỉnh.",
            false =>
                "Your answer was graded incorrect by the deterministic grader. Compare it with the suggested solution to identify the step to revise.",
            _ => response.Feedback
        };

        var errorType = preliminaryIsCorrect switch
        {
            true => ErrorType.None,
            false when response.ErrorType == ErrorType.None => ErrorType.Unknown,
            _ => response.ErrorType
        };

        return new ReasoningAnalysis
        {
            CenterId = centerId,
            AttemptId = attemptId,
            SchemaVersion = response.SchemaVersion,
            MethodDetected = response.MethodDetected,
            ReasoningQuality = response.ReasoningQuality,
            ErrorType = errorType,
            Misconception = preliminaryIsCorrect == true ? null : response.Misconception,
            MissingSteps = JsonSerializer.SerializeToDocument(
                preliminaryIsCorrect == true ? Array.Empty<string>() : response.MissingSteps),
            RootCauseNodeIds = JsonSerializer.SerializeToDocument(
                preliminaryIsCorrect == true ? Array.Empty<string>() : response.RootCauseNodeIds),
            AnalysisConfidence = response.Confidence,
            Feedback = deterministicFeedback,
            SolutionType = response.SolutionType,
            AiSolution = response.AiSolution,
            IsFallback = false,
            NeedsTeacherReview = false,
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
