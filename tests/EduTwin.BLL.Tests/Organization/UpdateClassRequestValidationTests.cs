using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Tests.Organization;

public class UpdateClassRequestValidationTests
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
        var request = new UpdateClassRequest
        {
            ClassName = null!,
            TeacherId = Guid.NewGuid(),
            Status = ClassStatus.Active,
            RowVersion = "1"
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("ClassName") && v.ErrorMessage != null);
    }

    [Fact]
    public void RowVersion_Null_FailsValidation()
    {
        var request = new UpdateClassRequest
        {
            ClassName = "Math",
            TeacherId = Guid.NewGuid(),
            Status = ClassStatus.Active,
            RowVersion = null!
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("RowVersion") && v.ErrorMessage != null);
    }

    [Fact]
    public void ClassName_ExceedsMaxLength_FailsValidation()
    {
        var request = new UpdateClassRequest
        {
            ClassName = new string('a', 151),
            TeacherId = Guid.NewGuid(),
            Status = ClassStatus.Active,
            RowVersion = "1"
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("ClassName") && v.ErrorMessage != null);
    }

    [Fact]
    public void Status_InvalidEnum_FailsValidation()
    {
        var request = new UpdateClassRequest
        {
            ClassName = "Math",
            TeacherId = Guid.NewGuid(),
            Status = (ClassStatus)999,
            RowVersion = "1"
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("Status") && v.ErrorMessage != null);
    }

    [Fact]
    public void Status_Null_FailsValidation()
    {
        var request = new UpdateClassRequest
        {
            ClassName = "Math",
            TeacherId = Guid.NewGuid(),
            Status = null,
            RowVersion = "1"
        };

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("Status") && v.ErrorMessage != null);
    }

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var request = new UpdateClassRequest
        {
            ClassName = "Math 101",
            TeacherId = Guid.NewGuid(),
            Status = ClassStatus.Archived,
            RowVersion = "1"
        };

        var results = ValidateModel(request);
        Assert.Empty(results);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [Fact]
    public void JsonDeserialize_MissingStatus_DeserializesToNull_AndFailsValidation()
    {
        var json = @"{ ""className"": ""Math"", ""teacherId"": ""00000000-0000-0000-0000-000000000000"", ""rowVersion"": ""1"" }";
        var request = JsonSerializer.Deserialize<UpdateClassRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Null(request.Status);

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("Status") && v.ErrorMessage != null);
    }

    [Fact]
    public void JsonDeserialize_NullStatus_DeserializesToNull_AndFailsValidation()
    {
        var json = @"{ ""className"": ""Math"", ""teacherId"": ""00000000-0000-0000-0000-000000000000"", ""status"": null, ""rowVersion"": ""1"" }";
        var request = JsonSerializer.Deserialize<UpdateClassRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Null(request.Status);

        var results = ValidateModel(request);
        Assert.Contains(results, v => v.MemberNames.Contains("Status") && v.ErrorMessage != null);
    }

    [Fact]
    public void JsonDeserialize_NumericStatus_ThrowsJsonException()
    {
        var json = @"{ ""className"": ""Math"", ""teacherId"": ""00000000-0000-0000-0000-000000000000"", ""status"": 0, ""rowVersion"": ""1"" }";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateClassRequest>(json, JsonOptions));
    }

    [Fact]
    public void JsonDeserialize_UnknownStringStatus_ThrowsJsonException()
    {
        var json = @"{ ""className"": ""Math"", ""teacherId"": ""00000000-0000-0000-0000-000000000000"", ""status"": ""Unknown"", ""rowVersion"": ""1"" }";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateClassRequest>(json, JsonOptions));
    }

    [Fact]
    public void JsonDeserialize_ValidStringStatus_DeserializesCorrectly_AndPassesValidation()
    {
        var json = @"{ ""className"": ""Math 101"", ""teacherId"": ""00000000-0000-0000-0000-000000000000"", ""status"": ""Active"", ""rowVersion"": ""1"" }";
        var request = JsonSerializer.Deserialize<UpdateClassRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(ClassStatus.Active, request.Status);

        var results = ValidateModel(request);
        Assert.Empty(results);
    }
}
