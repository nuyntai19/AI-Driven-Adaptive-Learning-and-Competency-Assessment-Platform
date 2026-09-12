using System;
using System.Collections.Generic;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public class ShortAnswerGrader : IQuestionGrader
{
    private readonly IMathAnswerNormalizer _mathNormalizer;

    public ShortAnswerGrader(IMathAnswerNormalizer mathNormalizer)
    {
        _mathNormalizer = mathNormalizer;
    }

    public ShortAnswerGrader() : this(new MathAnswerNormalizer())
    {
    }

    public PreliminaryGradingResult Grade(
        string? studentAnswer, 
        string? correctAnswer, 
        decimal maxScore, 
        GradingCriteria criteria, 
        IEnumerable<QuestionOption>? options = null)
    {
        return Grade(studentAnswer, correctAnswer, new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.TextExact,
            MaxScore = maxScore,
            Criteria = criteria,
            Options = options
        });
    }

    public PreliminaryGradingResult Grade(
        string? studentAnswer,
        string? correctAnswer,
        QuestionGradingContext context)
    {
        if (string.IsNullOrWhiteSpace(studentAnswer) || string.Equals(studentAnswer.Trim(), "SKIPPED", StringComparison.OrdinalIgnoreCase))
        {
            return new PreliminaryGradingResult
            {
                IsCorrect = false,
                Score = 0m,
                Feedback = "No answer provided (skipped)."
            };
        }

        // Mode: Manual
        if (context.EvaluationMode == QuestionAnswerEvaluationMode.Manual)
        {
            return new PreliminaryGradingResult
            {
                IsCorrect = null,
                Score = 0m,
                Feedback = "Requires teacher review."
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

        // Mode: NumericRational
        if (context.EvaluationMode == QuestionAnswerEvaluationMode.NumericRational)
        {
            if (!_mathNormalizer.TryNormalize(correctAnswer, out var expectedFraction))
            {
                return new PreliminaryGradingResult
                {
                    IsCorrect = null,
                    Score = 0m,
                    Feedback = "Requires manual review (invalid reference answer)."
                };
            }

            if (_mathNormalizer.TryNormalize(studentAnswer, out var studentFraction))
            {
                bool matches = studentFraction.Value.Numerator == expectedFraction.Value.Numerator &&
                               studentFraction.Value.Denominator == expectedFraction.Value.Denominator;

                return new PreliminaryGradingResult
                {
                    IsCorrect = matches,
                    Score = matches ? context.MaxScore : 0m,
                    Feedback = matches ? "Correct answer." : "Incorrect answer."
                };
            }

            // Unsupported mathematical grammar/format: cannot assume student is wrong.
            // Coercing to false would mutate mastery prematurely. Defer to teacher review.
            return new PreliminaryGradingResult
            {
                IsCorrect = null,
                Score = 0m,
                Feedback = "Unsupported mathematical format. Requires teacher review."
            };
        }

        // Mode: TextExact (Default)
        var normalizedStudentAnswer = studentAnswer.Trim();
        var normalizedCorrectAnswer = correctAnswer.Trim();

        bool isCorrect = string.Equals(normalizedStudentAnswer, normalizedCorrectAnswer, StringComparison.OrdinalIgnoreCase);

        return new PreliminaryGradingResult
        {
            IsCorrect = isCorrect,
            Score = isCorrect ? context.MaxScore : 0m,
            Feedback = isCorrect ? "Correct answer." : "Incorrect answer."
        };
    }
}
