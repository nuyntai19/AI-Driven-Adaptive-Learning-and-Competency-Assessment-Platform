using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Tests.Organization;

public class CreateTeacherRequestValidationTests
{
    private IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var model = new CreateTeacherRequest
        {
            Username = "teacher.01",
            TemporaryPassword = "ValidPassword123!",
            DisplayName = "Teacher One",
            Department = "Math"
        };
        var results = ValidateModel(model);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Username_RequiredOrWhitespace_FailsValidation(string? username)
    {
        var model = new CreateTeacherRequest
        {
            Username = username!,
            TemporaryPassword = "ValidPassword123!",
            DisplayName = "Teacher One"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateTeacherRequest.Username)));
    }

    [Fact]
    public void Username_MaxLength_FailsValidation()
    {
        var model = new CreateTeacherRequest
        {
            Username = new string('a', 101),
            TemporaryPassword = "ValidPassword123!",
            DisplayName = "Teacher One"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateTeacherRequest.Username)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678901")] // 11 chars
    public void TemporaryPassword_RequiredOrTooShort_FailsValidation(string? pwd)
    {
        var model = new CreateTeacherRequest
        {
            Username = "teacher.01",
            TemporaryPassword = pwd!,
            DisplayName = "Teacher One"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateTeacherRequest.TemporaryPassword)));
    }

    [Fact]
    public void TemporaryPassword_MaxLength_FailsValidation()
    {
        var model = new CreateTeacherRequest
        {
            Username = "teacher.01",
            TemporaryPassword = new string('a', 201),
            DisplayName = "Teacher One"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateTeacherRequest.TemporaryPassword)));
    }

    [Fact]
    public void TemporaryPassword_IsNotTrimmed()
    {
        var request = new CreateTeacherRequest
        {
            Username = "teacher.01",
            TemporaryPassword = "   ValidPassword123!   ",
            DisplayName = "Teacher One"
        };
        Assert.Equal("   ValidPassword123!   ", request.TemporaryPassword);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayName_RequiredOrWhitespace_FailsValidation(string? displayName)
    {
        var model = new CreateTeacherRequest
        {
            Username = "teacher.01",
            TemporaryPassword = "ValidPassword123!",
            DisplayName = displayName!
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateTeacherRequest.DisplayName)));
    }

    [Fact]
    public void DisplayName_MaxLength_FailsValidation()
    {
        var model = new CreateTeacherRequest
        {
            Username = "teacher.01",
            TemporaryPassword = "ValidPassword123!",
            DisplayName = new string('a', 201)
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateTeacherRequest.DisplayName)));
    }

    [Fact]
    public void Department_MaxLength_FailsValidation()
    {
        var model = new CreateTeacherRequest
        {
            Username = "teacher.01",
            TemporaryPassword = "ValidPassword123!",
            DisplayName = "Teacher One",
            Department = new string('a', 151)
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateTeacherRequest.Department)));
    }

    [Fact]
    public void Department_CanBeNull()
    {
        var model = new CreateTeacherRequest
        {
            Username = "teacher.01",
            TemporaryPassword = "ValidPassword123!",
            DisplayName = "Teacher One",
            Department = null
        };
        var results = ValidateModel(model);
        Assert.Empty(results);
    }

    [Fact]
    public void Username_IsTrimmed()
    {
        var request = new CreateTeacherRequest { Username = "  abc  " };
        Assert.Equal("abc", request.Username);
    }

    [Fact]
    public void DisplayName_IsTrimmed()
    {
        var request = new CreateTeacherRequest { DisplayName = "  name  " };
        Assert.Equal("name", request.DisplayName);
    }

    [Fact]
    public void Department_IsTrimmed()
    {
        var request = new CreateTeacherRequest { Department = "  math  " };
        Assert.Equal("math", request.Department);
    }

    [Fact]
    public void Department_WhitespaceBecomesNull()
    {
        var request = new CreateTeacherRequest { Department = "   " };
        Assert.Null(request.Department);
    }
}
