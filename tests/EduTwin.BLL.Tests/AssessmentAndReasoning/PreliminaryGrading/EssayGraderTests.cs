using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.Contracts.CurriculumAndQuestions;
using FluentAssertions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.PreliminaryGrading;

public class EssayGraderTests
{
    private readonly EssayGrader _sut = new();
    private readonly GradingCriteria _criteria = new();

    [Fact]
    public void Grade_AnyAnswer_ReturnsNullIsCorrectAndZeroScore()
    {
        var result = _sut.Grade("This is a long essay about history...", "", 10m, _criteria);
        
        result.IsCorrect.Should().BeNull();
        result.Score.Should().Be(0m);
        result.Feedback.Should().NotBeNullOrEmpty();
    }
}
