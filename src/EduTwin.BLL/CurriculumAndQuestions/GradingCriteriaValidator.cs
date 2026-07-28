using System;
using System.Collections.Generic;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class GradingCriteriaValidator
{
    public static List<string> Validate(GradingCriteria criteria)
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

        return errors;
    }
}
