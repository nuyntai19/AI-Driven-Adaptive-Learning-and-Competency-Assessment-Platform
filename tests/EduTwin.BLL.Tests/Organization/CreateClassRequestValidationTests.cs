using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Tests.Organization;

public class CreateClassRequestValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void ClassName_Null_FailsValidation()
    {
        var request = new CreateClassRequest
        {
            ClassName = null!,
            AcademicYear = "2026",
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid()
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("ClassName") && v.ErrorMessage != null);
    }

    [Fact]
    public void AcademicYear_Null_FailsValidation()
    {
        var request = new CreateClassRequest
        {
            ClassName = "Math",
            AcademicYear = null!,
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid()
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("AcademicYear") && v.ErrorMessage != null);
    }

    [Fact]
    public void ClassName_ExceedsMaxLength_FailsValidation()
    {
        var request = new CreateClassRequest
        {
            ClassName = new string('a', 151),
            AcademicYear = "2026",
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid()
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("ClassName") && v.ErrorMessage != null);
    }

    [Fact]
    public void AcademicYear_ExceedsMaxLength_FailsValidation()
    {
        var request = new CreateClassRequest
        {
            ClassName = "Math",
            AcademicYear = new string('a', 21),
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid()
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("AcademicYear") && v.ErrorMessage != null);
    }

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new CreateClassRequest
        {
            ClassName = "Math 101",
            AcademicYear = "2026-2027",
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid()
        };

        var results = ValidateModel(request);
        Assert.Empty(results);
    }
}
