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
                HintContent = BuildLevel1Hint(question, topicName),
                SupportLevelDescription = "Định hướng khái niệm chính. Điểm năng lực được bảo toàn 80%."
            },
            2 => new AdaptivePracticeHintDto
            {
                HintLevel = 2,
                HintTitle = "Gợi ý mức 2 – Nhắc lại quy tắc & công thức",
                HintContent = BuildLevel2Hint(question, topicName),
                SupportLevelDescription = "Gợi ý quy tắc và công thức cốt lõi. Điểm năng lực được tính với trọng số hỗ trợ 60%."
            },
            _ => new AdaptivePracticeHintDto
            {
                HintLevel = 3,
                HintTitle = "Gợi ý mức 3 – Hướng dẫn phương pháp giải",
                HintContent = BuildLevel3Hint(question),
                SupportLevelDescription = "Các bước giải chi tiết. Sau câu này hệ thống sẽ đưa ra câu tương đương để kiểm tra độ độc lập."
            }
        };
    }

    private static string BuildLevel1Hint(Question question, string topicName)
    {
        if (!string.IsNullOrWhiteSpace(question.ExpectedReasoning))
        {
            // First sentence or clause of reasoning
            var firstSentence = question.ExpectedReasoning.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (firstSentence.Length > 0 && !string.IsNullOrWhiteSpace(firstSentence[0]))
            {
                return $"Hãy chú ý đến bản chất của dạng bài: {firstSentence[0].Trim()}.";
            }
        }

        return $"Hãy xác định đại lượng cần tìm và các dữ kiện đã cho trong bài toán thuộc phần '{topicName}'. Cân nhắc thứ tự ưu tiên các phép biến đổi.";
    }

    private static string BuildLevel2Hint(Question question, string topicName)
    {
        if (!string.IsNullOrWhiteSpace(question.Solution))
        {
            // Extract intermediate formulas or rule mentions
            var lines = question.Solution.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1)
            {
                return $"Quy tắc trọng tâm: {lines[0].Trim()}\nÁp dụng biến đổi vế theo vế một cách cẩn thận.";
            }
        }

        return $"Nhắc lại kiến thức: Đối với {topicName}, lưu ý quy tắc dấu khi chuyển vế, quy tắc nhân/chia hai vế cho một số khác 0, và điều kiện xác định của biểu thức (nếu có).";
    }

    private static string BuildLevel3Hint(Question question)
    {
        if (!string.IsNullOrWhiteSpace(question.Solution))
        {
            // Mask the final answer in the solution so student still has to compute final result
            var sanitizedSolution = question.Solution;
            if (!string.IsNullOrWhiteSpace(question.CorrectAnswer))
            {
                sanitizedSolution = sanitizedSolution.Replace(question.CorrectAnswer, "[kết quả cần tìm]", StringComparison.OrdinalIgnoreCase);
            }

            return $"Hướng dẫn từng bước:\n{sanitizedSolution}\n\n-> Hãy tự tính toán và nhập đáp án chính xác cuối cùng.";
        }

        return "Bước 1: Đưa các số hạng chứa ẩn về một vế, các hằng số về vế còn lại.\nBước 2: Rút gọn hai vế.\nBước 3: Chia cả hai vế cho hệ số của ẩn để tìm nghiệm.";
    }
}
