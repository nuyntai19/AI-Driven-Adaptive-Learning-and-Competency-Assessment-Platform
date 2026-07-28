using System.Collections.Generic;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;
using FluentAssertions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.PreliminaryGrading;

public class MultipleChoiceGraderTests
{
    private readonly MultipleChoiceGrader _sut = new();
    private readonly GradingCriteria _criteria = new();

    [Fact]
    public void Grade_NullOrEmptyAnswer_ReturnsFalseAndZeroScore()
    {
        var result = _sut.Grade("", "A", 1m, _criteria);
        
        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0m);
    }

    [Fact]
    public void Grade_ExactMatchWithCorrectAnswer_ReturnsTrueAndMaxScore()
    {
        var result = _sut.Grade("A", "A", 1m, _criteria);
        
        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(1m);
    }

    [Fact]
    public void Grade_MatchOptionId_ReturnsTrueAndMaxScore()
    {
        var options = new List<QuestionOption>
        {
            new QuestionOption { OptionId = 1, OptionLabel = "A", IsCorrect = false },
            new QuestionOption { OptionId = 2, OptionLabel = "B", IsCorrect = true }
        };

        // Student selected option 2
        var result = _sut.Grade("2", "B", 2m, _criteria, options);
        
        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(2m);
    }

    [Fact]
    public void Grade_MatchOptionLabel_ReturnsTrueAndMaxScore()
    {
        var options = new List<QuestionOption>
        {
            new QuestionOption { OptionId = 1, OptionLabel = "A", IsCorrect = false },
            new QuestionOption { OptionId = 2, OptionLabel = "B", IsCorrect = true }
        };

        // Student selected option label 'b' (case insensitive match)
        var result = _sut.Grade("b", "B", 2m, _criteria, options);
        
        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(2m);
    }
    
    [Fact]
    public void Grade_WrongOptionId_ReturnsFalseAndZeroScore()
    {
        var options = new List<QuestionOption>
        {
            new QuestionOption { OptionId = 1, OptionLabel = "A", IsCorrect = false },
            new QuestionOption { OptionId = 2, OptionLabel = "B", IsCorrect = true }
        };

        var result = _sut.Grade("1", "B", 2m, _criteria, options);
        
        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0m);
    }
}
