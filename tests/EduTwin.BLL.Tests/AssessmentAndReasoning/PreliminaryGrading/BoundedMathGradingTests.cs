using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.PreliminaryGrading;

public sealed class BoundedMathGradingTests
{
    [Theory]
    [InlineData("D=R\\{2}", "R \\ {2}")]
    [InlineData(@"$D=\mathbb{R}\setminus\{2\}$", "ℝ \\ {2}")]
    [InlineData(@"\left\{1/2;2\right\}", "{2;0.5}")]
    [InlineData(@"\frac{1}{2}", "0.5")]
    [InlineData("R\\{2;2}", "R\\{2}")]
    [InlineData("R\\{}", "R")]
    [InlineData("{1 1/2;2}", "{1.5;2}")]
    public void EquivalentSupportedNotation_IsCorrect(string student, string reference)
    {
        var grade = Grade(student, reference);
        Assert.True(grade.IsCorrect);
        Assert.Equal(20m, grade.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.MathEquivalent, grade.ReasonCode);
    }

    [Theory]
    [InlineData("R\\{3}", "R\\{2}")]
    [InlineData("{2}", "R\\{2}")]
    [InlineData("{0.5;3}", "{0.5;2}")]
    public void DifferentValuesOrSets_AreNotAccepted(string student, string reference) => Assert.False(Grade(student, reference).IsCorrect);

    [Theory]
    [InlineData("x^2=4")]
    [InlineData(@"\sin(x)")]
    [InlineData(@"R\{\sqrt{2}}")]
    [InlineData(@"\placeholder{}")]
    [InlineData("R\\{1/0}")]
    public void UnsupportedSyntax_DefersInsteadOfMarkingIncorrect(string student)
    {
        var grade = Grade(student, "R\\{2}");
        Assert.Null(grade.IsCorrect);
        Assert.Null(grade.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.UnsupportedMathFormat, grade.ReasonCode);
    }

    [Fact]
    public void TextExact_RemainsLiteral_AndActivationRejectsUnboundedMath()
    {
        var grade = new ShortAnswerGrader().Grade("D=R\\{2}", "R \\ {2}", new QuestionGradingContext
        { EvaluationMode = QuestionAnswerEvaluationMode.TextExact, MaxScore = 20m });
        Assert.False(grade.IsCorrect);
        var policy = new QuestionActivationPolicy();
        Assert.True(policy.Validate(QuestionType.ShortAnswer, QuestionAnswerEvaluationMode.MathEquivalent, "R\\{2}", null, out _));
        Assert.False(policy.Validate(QuestionType.ShortAnswer, QuestionAnswerEvaluationMode.MathEquivalent, "sin(x)", null, out _));
        Assert.False(policy.Validate(QuestionType.MultipleChoice, QuestionAnswerEvaluationMode.MathEquivalent, "A", null, out _));
    }

    private static PreliminaryGradingResult Grade(string student, string reference) => new ShortAnswerGrader().Grade(student, reference,
        new QuestionGradingContext { EvaluationMode = QuestionAnswerEvaluationMode.MathEquivalent, MaxScore = 20m });
}
