using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EduTwin.Contracts.Organization;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class CreateStudentRequestValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    private CreateStudentRequest CreateValidRequest() => new()
    {
        Username = "student.001",
        TemporaryPassword = "change-me-123",
        FullName = "Tran Minh An",
        GradeLevel = 12,
        ClassIds = new List<Guid> { Guid.NewGuid() }
    };

    [Fact]
    public void Request_Valid_PassesValidation()
    {
        var request = CreateValidRequest();
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Username_MissingOrBlank_Fails(string? username)
    {
        var request = CreateValidRequest();
        request.Username = username!;
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.Username)));
    }

    [Fact]
    public void Username_Exceeds100Chars_Fails()
    {
        var request = CreateValidRequest();
        request.Username = new string('a', 101);
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.Username)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Password_MissingOrBlank_Fails(string? password)
    {
        var request = CreateValidRequest();
        request.TemporaryPassword = password!;
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.TemporaryPassword)));
    }

    [Theory]
    [InlineData("short")] // length 5
    [InlineData("abcdefghijk")] // length 11
    public void Password_Under12Chars_Fails(string password)
    {
        var request = CreateValidRequest();
        request.TemporaryPassword = password;
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.TemporaryPassword)));
    }

    [Fact]
    public void Password_Exceeds200Chars_Fails()
    {
        var request = CreateValidRequest();
        request.TemporaryPassword = new string('a', 201);
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.TemporaryPassword)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FullName_MissingOrBlank_Fails(string? fullName)
    {
        var request = CreateValidRequest();
        request.FullName = fullName!;
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.FullName)));
    }

    [Fact]
    public void FullName_Exceeds200Chars_Fails()
    {
        var request = CreateValidRequest();
        request.FullName = new string('a', 201);
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.FullName)));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void GradeLevel_OutsideRange_Fails(int gradeLevel)
    {
        var request = CreateValidRequest();
        request.GradeLevel = gradeLevel;
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.GradeLevel)));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void GradeLevel_InsideRange_Passes(int gradeLevel)
    {
        var request = CreateValidRequest();
        request.GradeLevel = gradeLevel;
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Fact]
    public void ClassIds_Null_Fails()
    {
        var request = CreateValidRequest();
        request.ClassIds = null!;
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.ClassIds)));
    }

    [Fact]
    public void ClassIds_ContainsGuidEmpty_Fails()
    {
        var request = CreateValidRequest();
        request.ClassIds = new List<Guid> { Guid.NewGuid(), Guid.Empty };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.ClassIds)));
    }

    [Fact]
    public void ClassIds_ContainsDuplicates_Fails()
    {
        var request = CreateValidRequest();
        var duplicateGuid = Guid.NewGuid();
        request.ClassIds = new List<Guid> { duplicateGuid, duplicateGuid };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateStudentRequest.ClassIds)));
    }

    [Fact]
    public void ClassIds_EmptyList_Passes()
    {
        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();
        var results = ValidateModel(request);
        Assert.Empty(results);
    }
}
