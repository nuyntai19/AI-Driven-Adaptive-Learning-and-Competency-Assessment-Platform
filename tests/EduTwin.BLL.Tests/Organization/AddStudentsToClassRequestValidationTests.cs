using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Tests.Organization;

public class AddStudentsToClassRequestValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, results, true);
        return results;
    }

    [Fact]
    public void Request_NullStudentIds_FailsValidation()
    {
        var request = new AddStudentsToClassRequest
        {
            StudentIds = null!
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("StudentIds"));
    }

    [Fact]
    public void Request_EmptyStudentIds_FailsValidation()
    {
        var request = new AddStudentsToClassRequest
        {
            StudentIds = Array.Empty<Guid>()
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("StudentIds"));
    }

    [Fact]
    public void Request_ContainsEmptyGuid_FailsValidation()
    {
        var request = new AddStudentsToClassRequest
        {
            StudentIds = new[] { Guid.NewGuid(), Guid.Empty }
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("StudentIds"));
    }

    [Fact]
    public void Request_ContainsDuplicateIds_FailsValidation()
    {
        var id = Guid.NewGuid();
        var request = new AddStudentsToClassRequest
        {
            StudentIds = new[] { id, id }
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains("StudentIds"));
    }

    [Fact]
    public void Request_ValidIds_PassesValidation()
    {
        var request = new AddStudentsToClassRequest
        {
            StudentIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
        };

        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Request_AllowGradeMismatchTrue_WithoutReason_FailsValidation(string? reason)
    {
        var request = new AddStudentsToClassRequest
        {
            StudentIds = new[] { Guid.NewGuid() },
            AllowGradeMismatch = true,
            GradeMismatchReason = reason
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AddStudentsToClassRequest.GradeMismatchReason)));
    }

    [Fact]
    public void Request_AllowGradeMismatchTrue_WithValidReason_PassesValidation()
    {
        var request = new AddStudentsToClassRequest
        {
            StudentIds = new[] { Guid.NewGuid() },
            AllowGradeMismatch = true,
            GradeMismatchReason = "Học sinh thi vượt cấp đạt chuẩn bồi dưỡng chuyên Toán."
        };

        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Fact]
    public void Request_GradeMismatchReasonTooLong_FailsValidation()
    {
        var request = new AddStudentsToClassRequest
        {
            StudentIds = new[] { Guid.NewGuid() },
            AllowGradeMismatch = true,
            GradeMismatchReason = new string('R', 501)
        };

        var results = ValidateModel(request);
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AddStudentsToClassRequest.GradeMismatchReason)));
    }
}
