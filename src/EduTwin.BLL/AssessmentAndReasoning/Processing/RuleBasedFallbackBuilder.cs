using System.Text.Json;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public sealed class RuleBasedFallbackBuilder : IRuleBasedFallbackBuilder
{
    public ReasoningAnalysis Build(RuleBasedFallbackInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CenterId == Guid.Empty)
        {
            throw new ArgumentException("Fallback input must have a resolved center.", nameof(input));
        }

        if (input.AttemptId == 0)
        {
            throw new ArgumentException("Fallback input must have a persisted attempt.", nameof(input));
        }

        var feedback = input.ReasoningLanguage switch
        {
            "vi" => BuildVietnameseFeedback(input.IsCorrect, input.Skipped),
            "en" => BuildEnglishFeedback(input.IsCorrect, input.Skipped),
            _ => throw new ArgumentException(
                "Reasoning language must be either vi or en.",
                nameof(input))
        };

        return new ReasoningAnalysis
        {
            CenterId = input.CenterId,
            AttemptId = input.AttemptId,
            SchemaVersion = "ai-analysis-v1",
            MethodDetected = null,
            ReasoningQuality = null,
            ErrorType = ErrorType.Unknown,
            Misconception = null,
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            AnalysisConfidence = null,
            Feedback = feedback,
            IsFallback = true,
            NeedsTeacherReview = true,
            Provider = AnalysisProvider.RuleBased,
            ModelName = null,
            OverrideReasoningQuality = null,
            OverrideErrorType = null,
            OverrideFeedback = null,
            OverrideIsCorrect = null,
            OverrideReason = null,
            OverriddenByUserId = null,
            OverriddenAt = null,
            OverrideVersion = 0,
            CreatedAt = input.UtcNow,
            CreatedBy = null,
            UpdatedAt = input.UtcNow
        };
    }

    private static string BuildVietnameseFeedback(bool? isCorrect, bool skipped)
    {
        if (skipped)
        {
            return "Bài làm đã được bỏ qua. Kết quả tạm thời cần giáo viên xem xét.";
        }

        return isCorrect switch
        {
            true => "Đáp án sơ bộ được chấm đúng. Phân tích tư duy tạm thời cần giáo viên xem xét.",
            false => "Đáp án sơ bộ được chấm chưa đúng. Phân tích tư duy tạm thời cần giáo viên xem xét.",
            null => "Chưa thể xác định tính đúng sai bằng chấm sơ bộ. Bài làm cần giáo viên xem xét."
        };
    }

    private static string BuildEnglishFeedback(bool? isCorrect, bool skipped)
    {
        if (skipped)
        {
            return "This submission was skipped. The temporary result requires teacher review.";
        }

        return isCorrect switch
        {
            true => "The preliminary answer was graded correct. The temporary reasoning analysis requires teacher review.",
            false => "The preliminary answer was graded incorrect. The temporary reasoning analysis requires teacher review.",
            null => "The preliminary grader could not determine correctness. This submission requires teacher review."
        };
    }
}
