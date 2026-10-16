using System;
using System.Collections.Generic;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

/// <summary>Persists option feedback alongside the question's grading criteria without a schema migration.</summary>
public static class QuestionOptionMisconceptionStore
{
    public static GradingCriteria Apply(GradingCriteria? criteria, IEnumerable<QuestionOptionInput>? options)
    {
        criteria ??= new GradingCriteria();
        var feedback = new Dictionary<string, string>(StringComparer.Ordinal);
        if (options is not null)
        {
            foreach (var option in options)
            {
                if (!string.IsNullOrWhiteSpace(option.OptionLabel) && !string.IsNullOrWhiteSpace(option.Misconception))
                    feedback[option.OptionLabel] = option.Misconception;
            }
        }
        return new GradingCriteria
        {
            SchemaVersion = criteria.SchemaVersion,
            RequiredIdeas = criteria.RequiredIdeas,
            CommonErrors = criteria.CommonErrors,
            ScoringNotes = criteria.ScoringNotes,
            Criteria = criteria.Criteria,
            OptionMisconceptions = feedback
        };
    }
}
