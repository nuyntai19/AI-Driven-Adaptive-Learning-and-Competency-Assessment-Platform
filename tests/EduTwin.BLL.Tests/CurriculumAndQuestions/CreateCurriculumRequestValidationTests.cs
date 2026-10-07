using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class CreateCurriculumRequestValidationTests
{
    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ValidRequest_PassesDataAnnotationsValidation()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Lộ trình Toán 12",
            Description = "Mô tả chi tiết",
            GradeLevel = 12,
            NodeIds = new List<string> { "100", "101" }
        };

        var errors = ValidateModel(request);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingOrNullTitle_FailsValidation(string? title)
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = title,
            GradeLevel = 10,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCurriculumRequest.Title)));
    }

    [Fact]
    public void WhitespaceOnlyTitle_FailsValidation()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "   ",
            GradeLevel = 10,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateCurriculumRequest.Title)));
    }

    [Fact]
    public void RawTitleLength250_PassesValidation()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = new string('X', 250),
            GradeLevel = 11,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void RawTitleLength251_FailsValidation()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = new string('X', 251),
            GradeLevel = 11,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCurriculumRequest.Title)));
    }

    [Fact]
    public void NullNodeIds_FailsValidation()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Valid Title",
            GradeLevel = 10,
            NodeIds = null
        };

        var errors = ValidateModel(request);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCurriculumRequest.NodeIds)));
    }

    [Fact]
    public void EmptyArrayNodeIds_PassesValidation()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Valid Title",
            GradeLevel = 10,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void LongDescription_DoesNotFailValidation()
    {
        var longDescription = new string('D', 5000);
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Valid Title",
            Description = longDescription,
            GradeLevel = 10,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void NullGradeLevel_FailsValidation()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Valid Title",
            GradeLevel = null,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCurriculumRequest.GradeLevel)));
    }

    [Theory]
    [InlineData((byte)9)]
    [InlineData((byte)13)]
    public void OutOfRangeGradeLevel_FailsValidation(byte gradeLevel)
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Valid Title",
            GradeLevel = gradeLevel,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateCurriculumRequest.GradeLevel)));
    }

    [Theory]
    [InlineData((byte)10)]
    [InlineData((byte)11)]
    [InlineData((byte)12)]
    public void ValidGradeLevels_PassValidation(byte gradeLevel)
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Valid Title",
            GradeLevel = gradeLevel,
            NodeIds = new List<string>()
        };

        var errors = ValidateModel(request);
        Assert.Empty(errors);
    }
}
