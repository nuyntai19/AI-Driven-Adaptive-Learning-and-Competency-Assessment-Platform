using System.Text;
using System.Text.RegularExpressions;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning;

/// <summary>Recognizes explicit non-answers, without imposing a minimum length on valid reasoning.</summary>
public static class ReasoningEvidencePolicy
{
    public const string MissingFeedback = "Chưa có nội dung lập luận để đánh giá cách giải. Đáp án được chấm riêng; chưa thể kết luận về phương pháp hoặc chất lượng lập luận của học sinh.";

    private static readonly HashSet<string> NonAnswers = new(StringComparer.Ordinal)
    {
        "khum", "khong biet", "em khong biet", "chua biet", "em chua biet", "ko biet", "k biet",
        "khong co lap luan", "chua co lap luan", "khong co loi giai", "chua lam", "em chua lam",
        "idk", "i dont know", "no reasoning", "no explanation", "na"
    };

    public static bool HasText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) normalized.Append(c == 'đ' ? 'd' : c);
            else if (char.IsWhiteSpace(c)) normalized.Append(' ');
        }
        var value = string.Join(' ', normalized.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        // Nonempty mathematical symbols may be meaningful (e.g. a concise inequality).
        return value.Length == 0 ? text.Any(char.IsSymbol) : !NonAnswers.Contains(value);
    }

    public static bool HasEvidence(string? reasoning, bool hasStudentImage,
        QuestionType questionType = QuestionType.ShortAnswer, string? finalAnswer = null) =>
        hasStudentImage || (HasText(reasoning) && !RestatesAnswer(reasoning!, finalAnswer))
        || (questionType == QuestionType.Essay && HasText(finalAnswer));

    /// <summary>
    /// One work box feeds both fields, so a student who writes only the result
    /// submits it as answer and as reasoning; that shows no method. "x = 7"
    /// restates 7, while "2x + 1 = 7" has an expression on the left and does not.
    /// </summary>
    private static bool RestatesAnswer(string reasoning, string? finalAnswer) =>
        !string.IsNullOrWhiteSpace(finalAnswer)
        && string.Equals(CompactAnswer(reasoning), CompactAnswer(finalAnswer), StringComparison.OrdinalIgnoreCase);

    private static string CompactAnswer(string value)
    {
        var compact = string.Concat(value.Where(c => c != '$' && !char.IsWhiteSpace(c)));
        return Regex.Replace(compact, @"^[A-Za-z]\w?=", "", RegexOptions.CultureInvariant);
    }

    /// <summary>The provider's way of saying the text gave nothing to assess (see the prompt's off-topic rule).</summary>
    public static bool ProviderFoundNoMethod(AnalyzeReasoningResponse response) =>
        response is { ReasoningVerdict: "Uncertain", ReasoningQuality: 0, MethodDetected: null };

    public static bool HasEvidence(AnalyzeReasoningRequest request) => HasEvidence(
        request.StudentSubmission.ReasoningText, request.StudentSubmission.ImageParts.Count > 0,
        request.Question.QuestionType, request.StudentSubmission.FinalAnswer);
}
