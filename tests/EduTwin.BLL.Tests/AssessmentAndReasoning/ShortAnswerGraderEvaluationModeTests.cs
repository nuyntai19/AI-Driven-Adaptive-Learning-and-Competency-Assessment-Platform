using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning;

public class ShortAnswerGraderEvaluationModeTests
{
    private readonly ShortAnswerGrader _grader = new(new MathAnswerNormalizer());

    [Fact]
    public void Grade_EmptyStudentAnswer_ReturnsIncorrectWithZeroScore()
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            MaxScore = 2.0m
        };

        var result = _grader.Grade("", "0.5", context);

        Assert.False(result.IsCorrect);
        Assert.Equal(0m, result.Score);
        Assert.Equal("No answer provided (skipped).", result.Feedback);
    }

    [Fact]
    public void Grade_ManualMode_ReturnsNullCorrectnessAndNullScore()
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.Manual,
            MaxScore = 3.0m
        };

        var result = _grader.Grade("x = 5", "5", context);

        Assert.Null(result.IsCorrect);
        Assert.Null(result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.ManualMode, result.ReasonCode);
        Assert.Equal("Requires teacher review.", result.Feedback);
    }

    [Theory]
    [InlineData("Paris", "paris", true)]
    [InlineData("  Hanoi  ", "HANOI", true)]
    [InlineData("Hanoi", "Saigon", false)]
    public void Grade_TextExactMode_PerformsTrimmedCaseInsensitiveComparison(string studentAnswer, string correctAnswer, bool expectedCorrect)
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.TextExact,
            MaxScore = 1.5m
        };

        var result = _grader.Grade(studentAnswer, correctAnswer, context);

        Assert.Equal(expectedCorrect, result.IsCorrect);
        Assert.Equal(expectedCorrect ? 1.5m : 0m, result.Score);
    }

    [Theory]
    [InlineData("1/2", "0.5", true)]
    [InlineData("0.5", "1/2", true)]
    [InlineData("2/4", "1/2", true)]
    [InlineData("1 1/2", "1.5", true)]
    [InlineData("-3/4", "-0.75", true)]
    [InlineData("0.33", "1/3", false)]
    [InlineData("2", "3", false)]
    public void Grade_NumericRationalMode_ComparesEquivalenceDeterministically(string studentAnswer, string correctAnswer, bool expectedCorrect)
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            MaxScore = 2.5m
        };

        var result = _grader.Grade(studentAnswer, correctAnswer, context);

        Assert.Equal(expectedCorrect, result.IsCorrect);
        Assert.Equal(expectedCorrect ? 2.5m : 0m, result.Score);
    }

    [Fact]
    public void Grade_NumericRationalMode_NonNumericStudentAnswer_RequiresTeacherReview()
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            MaxScore = 1.0m
        };

        var result = _grader.Grade("not a number", "1/2", context);

        Assert.Null(result.IsCorrect);
        Assert.Null(result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.UnsupportedMathFormat, result.ReasonCode);
        Assert.Contains("Unsupported mathematical format. Requires teacher review.", result.Feedback);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SKIPPED")]
    [InlineData("skipped")]
    public void Grade_NumericRationalMode_SkippedOrEmpty_ReturnsIncorrectWithoutTeacherReview(string answer)
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            MaxScore = 1.0m
        };

        var result = _grader.Grade(answer, "1/2", context);

        Assert.False(result.IsCorrect);
        Assert.Equal(0m, result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.NoAnswer, result.ReasonCode);
        Assert.Contains("No answer provided", result.Feedback);
    }

    [Fact]
    public void Grade_NumericRationalMode_InvalidReferenceAnswer_ReturnsManualReview()
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            MaxScore = 1.0m
        };

        var result = _grader.Grade("1/2", "non-parseable-answer", context);

        Assert.Null(result.IsCorrect);
        Assert.Null(result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.InvalidReferenceAnswer, result.ReasonCode);
        Assert.Contains("Requires manual review", result.Feedback);
    }

    [Theory]
    [InlineData("(1,1)", "(1, 1)")]
    [InlineData("(1;1)", "(1, 1)")]
    [InlineData("\\left(1,1\\right)", "(1;1)")]
    [InlineData("I(1;1)", "(1, 1)")]
    [InlineData("(1/2;2/4)", "(0.5;0.5)")]
    public void Grade_Coordinate2DMode_EquivalentCoordinates_ReturnsCorrect(
        string studentAnswer,
        string correctAnswer)
    {
        var result = _grader.Grade(studentAnswer, correctAnswer, new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.Coordinate2D,
            MaxScore = 40m
        });

        Assert.True(result.IsCorrect);
        Assert.Equal(40m, result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.CoordinateEquivalent, result.ReasonCode);
    }

    [Fact]
    public void Grade_Coordinate2DMode_DifferentCoordinate_ReturnsIncorrect()
    {
        var result = _grader.Grade("(2,1)", "(1,2)", new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.Coordinate2D,
            MaxScore = 40m
        });

        Assert.False(result.IsCorrect);
        Assert.Equal(0m, result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.CoordinateMismatch, result.ReasonCode);
    }

    [Fact]
    public void Grade_Coordinate2DMode_UnsupportedStudentFormat_ReturnsUnresolved()
    {
        var result = _grader.Grade("(1,1,1)", "(1,1)", new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.Coordinate2D,
            MaxScore = 40m
        });

        Assert.Null(result.IsCorrect);
        Assert.Null(result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.UnsupportedMathFormat, result.ReasonCode);
    }
}
