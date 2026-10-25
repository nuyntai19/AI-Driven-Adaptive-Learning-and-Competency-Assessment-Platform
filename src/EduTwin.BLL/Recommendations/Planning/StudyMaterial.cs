using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>Use the same actual worked examples when planning and opening a lesson.</summary>
public static class StudyMaterial
{
    public const string MissingMessage = "Chưa có bài học hoặc ví dụ có thể hiển thị cho kỹ năng này. Cần giáo viên bổ sung học liệu; bạn có thể tiếp tục phần luyện có hướng dẫn.";

    public static List<Question> Examples(IReadOnlyCollection<Question> pool, IReadOnlyCollection<ulong> used)
    {
        var withSolution = pool.Where(q => !string.IsNullOrWhiteSpace(q.Solution))
            .OrderBy(q => q.Difficulty).ThenBy(q => q.QuestionId).ToList();
        var examples = withSolution.Where(q => used.Contains(q.QuestionId)).Take(2).ToList();
        // Preserve five unseen check questions and two for guided practice.
        var spareUnseen = Math.Max(0, pool.Count(q => !used.Contains(q.QuestionId)) - 7);
        examples.AddRange(withSolution.Where(q => !used.Contains(q.QuestionId)).Take(Math.Min(2 - examples.Count, spareUnseen)));
        return examples;
    }

    public static bool Available(string? description, IReadOnlyCollection<Question> pool, IReadOnlyCollection<ulong> used)
        => !string.IsNullOrWhiteSpace(description) || Examples(pool, used).Count > 0;
}
