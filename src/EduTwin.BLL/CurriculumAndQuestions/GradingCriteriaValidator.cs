using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class GradingCriteriaValidator
{
    public static List<string> Validate(GradingCriteria criteria, decimal? maxScore = null)
    {
        var errors = new List<string>();

        if (criteria == null)
        {
            errors.Add("Grading criteria cannot be null.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(criteria.SchemaVersion))
        {
            errors.Add("SchemaVersion is required.");
        }

        if (criteria.RequiredIdeas == null)
        {
            errors.Add("RequiredIdeas cannot be null.");
        }

        if (criteria.CommonErrors == null)
        {
            errors.Add("CommonErrors cannot be null.");
        }

        if (criteria.Criteria == null)
        {
            errors.Add("Criteria cannot be null.");
        }
        else if (criteria.Criteria.Count > 0)
        {
            if (criteria.Criteria.Count > 20) errors.Add("At most 20 rubric criteria are allowed.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in criteria.Criteria)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.CriterionId) || item.CriterionId.Length > 64 ||
                    !ids.Add(item.CriterionId) || string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 200 ||
                    item.Description == null || item.Description.Length > 2000 || item.MaxScore <= 0m ||
                    item.MaxScore != Math.Round(item.MaxScore, 2))
                    errors.Add("Invalid or duplicate rubric criterion.");
                if (item != null && (item.VisualRequirements == null || item.VisualRequirements.Count > 12 ||
                    item.VisualRequirements.Any(r => string.IsNullOrWhiteSpace(r) || r.Length > 500) ||
                    item.VisualRequirements.Distinct(StringComparer.Ordinal).Count() != item.VisualRequirements.Count))
                    errors.Add("Visual requirements must be unique, nonempty, and at most 12 items of 500 characters per criterion.");
            }
            if (criteria.Criteria.All(c => c != null) && maxScore.HasValue && criteria.Criteria.Sum(c => c.MaxScore) != maxScore.Value)
                errors.Add("Rubric maximum scores must sum to the question maximum score.");
        }
        return errors;
    }
}
