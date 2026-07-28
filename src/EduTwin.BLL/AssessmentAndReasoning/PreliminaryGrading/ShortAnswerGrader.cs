using System;
using System.Collections.Generic;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public class ShortAnswerGrader : IQuestionGrader
{
    public PreliminaryGradingResult Grade(
        string? studentAnswer, 
        string? correctAnswer, 
        decimal maxScore, 
        GradingCriteria criteria, 
        IEnumerable<QuestionOption>? options = null)
    {
        if (string.IsNullOrWhiteSpace(studentAnswer))
        {
            return new PreliminaryGradingResult
            {
                IsCorrect = false,
                Score = 0m,
                Feedback = "No answer provided."
            };
        }

        if (string.IsNullOrWhiteSpace(correctAnswer))
        {
            // If there's no correct answer to compare against, we can't grade it deterministically.
            return new PreliminaryGradingResult
            {
                IsCorrect = null,
                Score = 0m,
                Feedback = "Requires manual review."
            };
        }

        // Normalize exact / canonical comparison MVP: Trim and Case-insensitive comparison
        var normalizedStudentAnswer = studentAnswer.Trim();
        var normalizedCorrectAnswer = correctAnswer.Trim();

        bool isCorrect = string.Equals(normalizedStudentAnswer, normalizedCorrectAnswer, StringComparison.OrdinalIgnoreCase);

        return new PreliminaryGradingResult
        {
            IsCorrect = isCorrect,
            Score = isCorrect ? maxScore : 0m,
            Feedback = isCorrect ? "Correct answer." : "Incorrect answer."
        };
    }
}
