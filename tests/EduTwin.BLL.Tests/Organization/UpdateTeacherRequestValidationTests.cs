using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using System.Text.Json;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class UpdateTeacherRequestValidationTests
{
    private IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) },
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new UpdateTeacherRequest
        {
            DisplayName = "Valid Name",
            Department = "Math",
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void DisplayName_Required(string? name)
    {
        var request = new UpdateTeacherRequest
        {
            DisplayName = name!,
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTeacherRequest.DisplayName)));
    }

    [Fact]
    public void DisplayName_Exceeds200_Fails()
    {
        var request = new UpdateTeacherRequest
        {
            DisplayName = new string('A', 201),
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTeacherRequest.DisplayName)));
    }

    [Fact]
    public void Department_Exceeds150_Fails()
    {
        var request = new UpdateTeacherRequest
        {
            DisplayName = "Valid",
            Department = new string('B', 151),
            Status = UserStatus.Active,
            RowVersion = "1"
        };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTeacherRequest.Department)));
    }

    [Fact]
    public void Status_UndefinedEnum_Fails()
    {
        var request = new UpdateTeacherRequest
        {
            DisplayName = "Valid",
            Status = (UserStatus)999,
            RowVersion = "1"
        };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTeacherRequest.Status)));
    }

    [Fact]
    public void MissingStatus_FailsValidation()
    {
        var json = """{"displayName": "Valid", "rowVersion": "1"}""";
        var request = JsonSerializer.Deserialize<UpdateTeacherRequest>(json, _jsonOptions)!;
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTeacherRequest.Status)));
    }

    [Fact]
    public void NullStatus_FailsValidation()
    {
        var json = """{"displayName": "Valid", "status": null, "rowVersion": "1"}""";
        var request = JsonSerializer.Deserialize<UpdateTeacherRequest>(json, _jsonOptions)!;
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTeacherRequest.Status)));
    }

    [Fact]
    public void StringStatusActive_DeserializesAndValidates()
    {
        var json = """{"displayName": "Valid", "status": "Active", "rowVersion": "1"}""";
        var request = JsonSerializer.Deserialize<UpdateTeacherRequest>(json, _jsonOptions)!;
        var results = ValidateModel(request);
        Assert.Empty(results);
        Assert.Equal(UserStatus.Active, request.Status);
    }

    [Fact]
    public void NumericStatus_IsRejectedByJsonConfiguration()
    {
        var json = """{"displayName": "Valid", "status": 0, "rowVersion": "1"}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateTeacherRequest>(json, _jsonOptions));
    }

    [Fact]
    public void UnknownStringStatus_IsRejected()
    {
        var json = """{"displayName": "Valid", "status": "InvalidStatus", "rowVersion": "1"}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateTeacherRequest>(json, _jsonOptions));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RowVersion_Required(string? version)
    {
        var request = new UpdateTeacherRequest
        {
            DisplayName = "Valid",
            Status = UserStatus.Active,
            RowVersion = version!
        };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateTeacherRequest.RowVersion)));
    }

    [Fact]
    public void DisplayName_IsTrimmed()
    {
        var request = new UpdateTeacherRequest { DisplayName = "  name  " };
        Assert.Equal("name", request.DisplayName);
    }

    [Fact]
    public void Department_IsTrimmed()
    {
        var request = new UpdateTeacherRequest { Department = "  math  " };
        Assert.Equal("math", request.Department);
    }

    [Fact]
    public void Department_WhitespaceBecomesNull()
    {
        var request = new UpdateTeacherRequest { Department = "   " };
        Assert.Null(request.Department);
    }
}
