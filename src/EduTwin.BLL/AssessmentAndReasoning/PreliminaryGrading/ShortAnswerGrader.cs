using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public class ShortAnswerGrader : IQuestionGrader
{
    private readonly IMathAnswerNormalizer _mathNormalizer;
    private readonly ICoordinateAnswerNormalizer _coordinateNormalizer;

    public ShortAnswerGrader(
        IMathAnswerNormalizer mathNormalizer,
        ICoordinateAnswerNormalizer coordinateNormalizer)
    {
        _mathNormalizer = mathNormalizer;
        _coordinateNormalizer = coordinateNormalizer;
    }

    public ShortAnswerGrader(IMathAnswerNormalizer mathNormalizer)
        : this(mathNormalizer, new CoordinateAnswerNormalizer(mathNormalizer))
    {
    }

    public ShortAnswerGrader()
        : this(new MathAnswerNormalizer())
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
                Feedback = "No answer provided (skipped).",
                ReasonCode = PreliminaryGradingReasonCodes.NoAnswer
            };
        }

        // Mode: Manual
        if (context.EvaluationMode == QuestionAnswerEvaluationMode.Manual)
        {
            return new PreliminaryGradingResult
            {
                IsCorrect = null,
                Score = null,
                Feedback = "Requires teacher review.",
                ReasonCode = PreliminaryGradingReasonCodes.ManualMode
            };
        }

        if (string.IsNullOrWhiteSpace(correctAnswer))
        {
            // If there's no correct answer to compare against, we can't grade it deterministically.
            return new PreliminaryGradingResult
            {
                IsCorrect = null,
                Score = null,
                Feedback = "Requires manual review.",
                ReasonCode = PreliminaryGradingReasonCodes.InvalidReferenceAnswer
            };
        }

        if (context.EvaluationMode == QuestionAnswerEvaluationMode.Coordinate2D)
        {
            if (!_coordinateNormalizer.TryNormalize(correctAnswer, out var expectedCoordinate))
            {
                return new PreliminaryGradingResult
                {
                    IsCorrect = null,
                    Score = null,
                    Feedback = "Requires manual review (invalid coordinate reference answer).",
                    ReasonCode = PreliminaryGradingReasonCodes.InvalidReferenceAnswer
                };
            }

            if (!_coordinateNormalizer.TryNormalize(studentAnswer, out var studentCoordinate))
            {
                return new PreliminaryGradingResult
                {
                    IsCorrect = null,
                    Score = null,
                    Feedback = "Unsupported coordinate format. Requires teacher review.",
                    ReasonCode = PreliminaryGradingReasonCodes.UnsupportedMathFormat
                };
            }

            var matches = studentCoordinate.GetValueOrDefault() == expectedCoordinate.GetValueOrDefault();
            return new PreliminaryGradingResult
            {
                IsCorrect = matches,
                Score = matches ? context.MaxScore : 0m,
                Feedback = matches ? "Correct coordinate." : "Incorrect coordinate.",
                ReasonCode = matches
                    ? PreliminaryGradingReasonCodes.CoordinateEquivalent
                    : PreliminaryGradingReasonCodes.CoordinateMismatch
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
                    Score = null,
                    Feedback = "Requires manual review (invalid reference answer).",
                    ReasonCode = PreliminaryGradingReasonCodes.InvalidReferenceAnswer
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
                    Feedback = matches ? "Correct answer." : "Incorrect answer.",
                    ReasonCode = matches
                        ? PreliminaryGradingReasonCodes.NumericEquivalent
                        : PreliminaryGradingReasonCodes.NumericMismatch
                };
            }

            // Unsupported mathematical grammar/format: cannot assume student is wrong.
            // Coercing to false would mutate mastery prematurely. Defer to teacher review.
            return new PreliminaryGradingResult
            {
                IsCorrect = null,
                Score = null,
                Feedback = "Unsupported mathematical format. Requires teacher review.",
                ReasonCode = PreliminaryGradingReasonCodes.UnsupportedMathFormat
            };
        }

        // Mode: TextExact (Default)
        var normalizedStudentAnswer = studentAnswer.Trim();
        var normalizedCorrectAnswer = correctAnswer.Trim();

        bool isCorrect = string.Equals(normalizedStudentAnswer, normalizedCorrectAnswer, StringComparison.OrdinalIgnoreCase);
        var reasonCode = isCorrect
            ? PreliminaryGradingReasonCodes.ExactMatch
            : AreEqualIgnoringWhitespace(normalizedStudentAnswer, normalizedCorrectAnswer)
                ? PreliminaryGradingReasonCodes.TextMismatchInternalWhitespace
                : PreliminaryGradingReasonCodes.TextMismatch;

        return new PreliminaryGradingResult
        {
            IsCorrect = isCorrect,
            Score = isCorrect ? context.MaxScore : 0m,
            Feedback = isCorrect ? "Correct answer." : "Incorrect answer.",
            ReasonCode = reasonCode
        };
    }

    private static bool AreEqualIgnoringWhitespace(string left, string right)
    {
        static string RemoveWhitespace(string value) =>
            string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

        return string.Equals(
            RemoveWhitespace(left),
            RemoveWhitespace(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
