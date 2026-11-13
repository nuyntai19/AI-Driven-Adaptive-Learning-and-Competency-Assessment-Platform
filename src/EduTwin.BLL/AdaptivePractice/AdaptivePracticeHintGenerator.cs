using System;
using System.Text.RegularExpressions;
using EduTwin.Contracts.AdaptivePractice;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.BLL.AdaptivePractice;

public static class AdaptivePracticeHintGenerator
{
    public static AdaptivePracticeHintDto GenerateHint(Question question, KnowledgeNode? topicNode, int hintLevel)
    {
        var level = Math.Clamp(hintLevel, 1, 3);
        var topicName = topicNode?.Name ?? "Chủ đề hiện tại";

        return level switch
        {
            1 => new AdaptivePracticeHintDto
            {
                HintLevel = 1,
                HintTitle = "Gợi ý mức 1 – Định hướng tư duy",
                HintContent = BuildLevel1Hint(topicName),
                SupportLevelDescription = "Nếu làm đúng sau gợi ý này, năng lực của chủ đề tăng 2,5%."
            },
            2 => new AdaptivePracticeHintDto
            {
                HintLevel = 2,
                HintTitle = "Gợi ý mức 2 – Nhắc lại quy tắc & công thức",
                HintContent = BuildLevel2Hint(question, topicName),
                SupportLevelDescription = "Nếu làm đúng sau gợi ý này, năng lực của chủ đề tăng 1%."
            },
            _ => new AdaptivePracticeHintDto
            {
                HintLevel = 3,
                HintTitle = "Gợi ý mức 3 – Hướng dẫn phương pháp giải",
                HintContent = BuildLevel3Hint(question),
                SupportLevelDescription = "Nếu làm đúng sau gợi ý này, năng lực của chủ đề tăng 1%."
            }
        };
    }

    // Level 1 only orients the student: the expected reasoning can already
    // contain the computation, so it must never be quoted here.
    private static string BuildLevel1Hint(string topicName) =>
        $"Đọc kỹ đề và xác định dạng bài thuộc phần '{topicName}': đề đã cho những gì và cần tìm điều gì?";

    private static string BuildLevel2Hint(Question question, string topicName)
    {
        if (!string.IsNullOrWhiteSpace(question.Solution))
        {
            var lines = question.Solution.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1 && !ContainsAnswer(lines[0], question.CorrectAnswer))
            {
                return $"Quy tắc trọng tâm: {lines[0].Trim()}";
            }
        }

        return $"Nhắc lại kiến thức chính của phần '{topicName}' rồi áp dụng vào dữ kiện của đề.";
    }

    private static string BuildLevel3Hint(Question question)
    {
        if (!string.IsNullOrWhiteSpace(question.Solution))
        {
            return $"Hướng dẫn từng bước:\n{MaskAnswer(question.Solution, question.CorrectAnswer)}\n\nHãy tự hoàn thành bước cuối và nhập đáp án.";
        }

        return "Bước 1: Viết ra dữ kiện và điều cần tìm.\nBước 2: Chọn kiến thức phù hợp để liên hệ chúng.\nBước 3: Thực hiện từng bước và kiểm tra lại kết quả.";
    }

    // A short answer such as "0" must only match as a whole token, or every
    // zero in the solution would be blanked out.
    private static Regex AnswerToken(string answer) =>
        new($@"(?<![\p{{L}}\p{{N}}.,]){Regex.Escape(answer.Trim())}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));

    private static bool ContainsAnswer(string text, string? answer) =>
        !string.IsNullOrWhiteSpace(answer) && AnswerToken(answer).IsMatch(text);

    // Inside a formula the answer becomes a box, in prose a phrase: "[kết quả cần tìm]" typed inside $...$ was
    // rendered by KaTeX as garbled math letters.
    private static string MaskAnswer(string text, string? answer) =>
        string.IsNullOrWhiteSpace(answer) ? text : EduTwin.BLL.AssessmentAndReasoning.MathAwareMask.Replace(text, AnswerToken(answer), "[kết quả cần tìm]");
}
