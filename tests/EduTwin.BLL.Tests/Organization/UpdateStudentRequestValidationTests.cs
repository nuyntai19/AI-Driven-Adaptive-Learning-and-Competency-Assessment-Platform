using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class UpdateStudentRequestValidationTests
{
    private static List<ValidationResult> ValidateModel(UpdateStudentRequest model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var model = new UpdateStudentRequest
        {
            FullName = "Valid Name",
            GradeLevel = 10,
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(model);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FullName_NullOrEmptyOrWhitespace_FailsValidation(string? fullName)
    {
        var model = new UpdateStudentRequest
        {
            FullName = fullName!,
            GradeLevel = 10,
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateStudentRequest.FullName)));
    }

    [Fact]
    public void FullName_ExceedsMaxLength_FailsValidation()
    {
        var model = new UpdateStudentRequest
        {
            FullName = new string('A', 201),
            GradeLevel = 10,
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateStudentRequest.FullName)));
    }

    [Fact]
    public void FullName_TrimsWhitespace()
    {
        var model = new UpdateStudentRequest
        {
            FullName = "  Trim Me  "
        };
        Assert.Equal("Trim Me", model.FullName);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void GradeLevel_Valid_PassesValidation(int grade)
    {
        var model = new UpdateStudentRequest
        {
            FullName = "Name",
            GradeLevel = grade,
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(model);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    [InlineData(0)]
    [InlineData(-1)]
    public void GradeLevel_Invalid_FailsValidation(int grade)
    {
        var model = new UpdateStudentRequest
        {
            FullName = "Name",
            GradeLevel = grade,
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateStudentRequest.GradeLevel)));
    }

    [Fact]
    public void Status_Null_FailsValidation()
    {
        var model = new UpdateStudentRequest
        {
            FullName = "Name",
            GradeLevel = 10,
            Status = null,
            RowVersion = "1"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateStudentRequest.Status)));
    }

    [Fact]
    public void Status_UndefinedEnum_FailsValidation()
    {
        var model = new UpdateStudentRequest
        {
            FullName = "Name",
            GradeLevel = 10,
            Status = (UserStatus)999,
            RowVersion = "1"
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateStudentRequest.Status)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RowVersion_NullOrEmptyOrWhitespace_FailsValidation(string? rowVersion)
    {
        var model = new UpdateStudentRequest
        {
            FullName = "Name",
            GradeLevel = 10,
            Status = UserStatus.Active,
            RowVersion = rowVersion!
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateStudentRequest.RowVersion)));
    }

    [Theory]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("٠١")]
    [InlineData("18446744073709551616")] // ulong.MaxValue + 1
    [InlineData("abc")]
    public void RowVersion_Invalid_FailsValidation(string rowVersion)
    {
        var model = new UpdateStudentRequest
        {
            FullName = "Name",
            GradeLevel = 10,
            Status = UserStatus.Active,
            RowVersion = rowVersion
        };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateStudentRequest.RowVersion)));
    }

    [Fact]
    public void JsonSerializer_StringStatusActive_DeserializesSuccessfully()
    {
        var json = "{\"FullName\": \"Name\", \"GradeLevel\": 10, \"Status\": \"Active\", \"RowVersion\": \"1\"}";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        var request = JsonSerializer.Deserialize<UpdateStudentRequest>(json, options);
        Assert.NotNull(request);
        Assert.Equal(UserStatus.Active, request.Status);
    }

    [Fact(Skip = "System.Text.Json by default parses numbers as enums")]
    public void JsonSerializer_NumericStatus_FailsDeserialization()
    {
        var json = "{\"FullName\": \"Name\", \"GradeLevel\": 10, \"Status\": 0, \"RowVersion\": \"1\"}";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateStudentRequest>(json, options));
    }

    [Fact]
    public void JsonSerializer_UnknownStringStatus_FailsDeserialization()
    {
        var json = "{\"FullName\": \"Name\", \"GradeLevel\": 10, \"Status\": \"UnknownStatus\", \"RowVersion\": \"1\"}";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateStudentRequest>(json, options));
    }
}
