using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public class MultipleChoiceGrader : IQuestionGrader
{
    public PreliminaryGradingResult Grade(
        string? studentAnswer, 
        string? correctAnswer, 
        decimal maxScore, 
        GradingCriteria criteria, 
        IEnumerable<QuestionOption>? options = null)
    {
        // For Multiple Choice, the studentAnswer could be the OptionId or OptionLabel.
        // The correctAnswer is the canonical label of the correct option or its ID.
        // We will assume exact string match for ID or Label.

        if (string.IsNullOrWhiteSpace(studentAnswer))
        {
            return new PreliminaryGradingResult
            {
                IsCorrect = false,
                Score = 0m,
                Feedback = "No answer provided."
            };
        }

        bool isCorrect = false;

        if (string.IsNullOrWhiteSpace(correctAnswer))
        {
            return new PreliminaryGradingResult
            {
                IsCorrect = null,
                Score = 0m,
                Feedback = "Requires manual review (no correct answer provided)."
            };
        }

        // Try exact match with the correct answer from question
        if (string.Equals(studentAnswer.Trim(), correctAnswer.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            isCorrect = true;
        }
        else if (options != null)
        {
            // Or try to match against options if provided
            var selectedOption = options.FirstOrDefault(o => 
                string.Equals(o.OptionLabel, studentAnswer.Trim(), StringComparison.OrdinalIgnoreCase) || 
                o.OptionId.ToString() == studentAnswer.Trim());

            if (selectedOption != null && selectedOption.IsCorrect)
            {
                isCorrect = true;
            }
        }

        return new PreliminaryGradingResult
        {
            IsCorrect = isCorrect,
            Score = isCorrect ? maxScore : 0m,
            Feedback = isCorrect ? "Correct answer." : "Incorrect answer."
        };
    }
}
