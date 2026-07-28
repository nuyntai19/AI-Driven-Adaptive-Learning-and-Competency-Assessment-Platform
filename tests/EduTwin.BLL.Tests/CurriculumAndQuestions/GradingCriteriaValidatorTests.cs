using System.Collections.Generic;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.Contracts.CurriculumAndQuestions;
using FluentAssertions;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class GradingCriteriaValidatorTests
{
    [Fact]
    public void Validate_NullCriteria_ReturnsError()
    {
        var errors = GradingCriteriaValidator.Validate(null!);
        
        errors.Should().ContainSingle();
        errors[0].Should().Contain("cannot be null");
    }

    [Fact]
    public void Validate_ValidCriteria_ReturnsEmptyErrors()
    {
        var criteria = new GradingCriteria
        {
            SchemaVersion = "1.0",
            RequiredIdeas = new List<string> { "Idea 1" },
            CommonErrors = new List<string> { "Error 1" }
        };

        var errors = GradingCriteriaValidator.Validate(criteria);
        
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingSchemaVersion_ReturnsError()
    {
        var criteria = new GradingCriteria
        {
            SchemaVersion = "",
            RequiredIdeas = new List<string>(),
            CommonErrors = new List<string>()
        };

        var errors = GradingCriteriaValidator.Validate(criteria);
        
        errors.Should().Contain(e => e.Contains("SchemaVersion is required"));
    }
    
    [Fact]
    public void Validate_NullLists_ReturnsErrors()
    {
        var criteria = new GradingCriteria
        {
            SchemaVersion = "1.0",
            RequiredIdeas = null!,
            CommonErrors = null!
        };

        var errors = GradingCriteriaValidator.Validate(criteria);
        
        errors.Should().HaveCount(2);
        errors.Should().Contain(e => e.Contains("RequiredIdeas"));
        errors.Should().Contain(e => e.Contains("CommonErrors"));
    }
}
