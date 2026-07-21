using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class CreateSubjectRequestValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidSubjectCode_ReturnsValidationFailed(string? code)
    {
        var request = new CreateSubjectRequest { SubjectCode = code!, SubjectName = "Valid" };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void SubjectCode_Exceeds32_ReturnsValidationFailed()
    {
        var request = new CreateSubjectRequest { SubjectCode = new string('A', 33), SubjectName = "Valid" };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidSubjectName_ReturnsValidationFailed(string? name)
    {
        var request = new CreateSubjectRequest { SubjectCode = "Valid", SubjectName = name! };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void SubjectName_Exceeds100_ReturnsValidationFailed()
    {
        var request = new CreateSubjectRequest { SubjectCode = "Valid", SubjectName = new string('A', 101) };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void Description_Exceeds500_ReturnsValidationFailed()
    {
        var request = new CreateSubjectRequest { SubjectCode = "Valid", SubjectName = "Valid", Description = new string('A', 501) };
        var results = ValidateModel(request);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void ValidRequest_IsValid()
    {
        var request = new CreateSubjectRequest { SubjectCode = "Valid", SubjectName = "Valid" };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmptyDescription_IsAllowed(string? desc)
    {
        var request = new CreateSubjectRequest { SubjectCode = "Valid", SubjectName = "Valid", Description = desc };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }
}
