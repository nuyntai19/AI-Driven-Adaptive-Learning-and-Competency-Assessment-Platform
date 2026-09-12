using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
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
        Assert.Equal("No answer provided.", result.Feedback);
    }

    [Fact]
    public void Grade_ManualMode_ReturnsNullCorrectnessWithZeroScore()
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.Manual,
            MaxScore = 3.0m
        };

        var result = _grader.Grade("x = 5", "5", context);

        Assert.Null(result.IsCorrect);
        Assert.Equal(0m, result.Score);
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
    public void Grade_NumericRationalMode_NonNumericStudentAnswer_ReturnsIncorrect()
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            MaxScore = 1.0m
        };

        var result = _grader.Grade("not a number", "1/2", context);

        Assert.False(result.IsCorrect);
        Assert.Equal(0m, result.Score);
        Assert.Contains("cannot evaluate as a numeric rational value", result.Feedback);
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
        Assert.Equal(0m, result.Score);
        Assert.Contains("Requires manual review", result.Feedback);
    }
}
