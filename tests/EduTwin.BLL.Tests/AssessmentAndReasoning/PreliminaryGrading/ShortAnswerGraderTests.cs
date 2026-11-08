using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using FluentAssertions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.PreliminaryGrading;

public class ShortAnswerGraderTests
{
    private readonly ShortAnswerGrader _sut = new();
    private readonly GradingCriteria _criteria = new();

    [Theory]
    [InlineData("She has left", "She has left.", true)]
    [InlineData("She has left.", "She has left", true)]
    [InlineData("went!", "went", true)]
    [InlineData("Is he a doctor", "Is he a doctor?", true)]
    [InlineData("She has lef", "She has left.", false)]
    [InlineData("She. has left", "She has left", false)]
    [InlineData("...", "...", true)]
    public void Grade_TextExact_IgnoresSentencePunctuationAtTheEndOnly(string answer, string reference, bool expected)
    {
        _sut.Grade(answer, reference, 1m, _criteria).IsCorrect.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Grade_NullOrEmptyAnswer_ReturnsFalseAndZeroScore(string? answer)
    {
        var result = _sut.Grade(answer, "Correct", 1m, _criteria);
        
        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0m);
        result.ReasonCode.Should().Be(PreliminaryGradingReasonCodes.NoAnswer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Grade_NullOrEmptyCorrectAnswer_ReturnsNullIsCorrect(string? correctAnswer)
    {
        var result = _sut.Grade("Student Answer", correctAnswer, 1m, _criteria);
        
        result.IsCorrect.Should().BeNull();
        result.Score.Should().BeNull();
        result.ReasonCode.Should().Be(PreliminaryGradingReasonCodes.InvalidReferenceAnswer);
    }

    [Theory]
    [InlineData("Correct", "Correct")]
    [InlineData(" correct ", "Correct")]
    [InlineData("CORRECT", "correct")]
    public void Grade_MatchingAnswer_ReturnsTrueAndMaxScore(string studentAnswer, string correctAnswer)
    {
        var result = _sut.Grade(studentAnswer, correctAnswer, 2m, _criteria);
        
        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(2m);
    }

    [Theory]
    [InlineData("Wrong", "Correct")]
    [InlineData("almost", "almost correct")]
    public void Grade_NonMatchingAnswer_ReturnsFalseAndZeroScore(string studentAnswer, string correctAnswer)
    {
        var result = _sut.Grade(studentAnswer, correctAnswer, 2m, _criteria);
        
        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0m);
    }
}
