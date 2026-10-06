using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.Override;

public static class RubricGrading
{
    public static bool TryGrade(GradingCriteria definition, decimal maxScore, IReadOnlyList<RubricScoreInput>? scores,
        out RubricGrade? grade, out string? error)
    {
        grade = null;
        error = null;
        if (definition.Criteria == null)
        {
            error = "Rubric không hợp lệ.";
            return false;
        }
        if (definition.Criteria.Count == 0)
        {
            if (scores is { Count: > 0 }) error = "Câu hỏi không có rubric tính điểm.";
            return error == null;
        }
        if (GradingCriteriaValidator.Validate(definition, maxScore).Count > 0 || scores == null || scores.Count != definition.Criteria.Count)
        {
            error = "Hãy chấm đủ các tiêu chí của rubric hợp lệ.";
            return false;
        }
        var results = new List<RubricCriterionGrade>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var score in scores)
        {
            var criterion = definition.Criteria.FirstOrDefault(c => c.CriterionId == score?.CriterionId);
            if (score == null || criterion == null || !seen.Add(score.CriterionId) || score.AwardedScore < 0m ||
                score.AwardedScore > criterion.MaxScore || score.AwardedScore != Math.Round(score.AwardedScore, 2) || score.Comment?.Length > 2000)
            {
                error = "Điểm tiêu chí không hợp lệ, bị trùng hoặc vượt điểm tối đa.";
                return false;
            }
            results.Add(new RubricCriterionGrade { CriterionId = criterion.CriterionId, Title = criterion.Title,
                Description = criterion.Description, MaxScore = criterion.MaxScore, AwardedScore = score.AwardedScore, Comment = score.Comment?.Trim() });
        }
        grade = new RubricGrade { MaxScore = maxScore, AwardedScore = results.Sum(c => c.AwardedScore),
            Criteria = definition.Criteria.Select(c => results.Single(r => r.CriterionId == c.CriterionId)).ToList() };
        return true;
    }
}
