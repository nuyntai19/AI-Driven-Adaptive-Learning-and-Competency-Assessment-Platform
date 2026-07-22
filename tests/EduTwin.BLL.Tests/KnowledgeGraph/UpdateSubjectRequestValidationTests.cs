using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EduTwin.Contracts.KnowledgeGraph;
using Xunit;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class UpdateSubjectRequestValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    // 1. Valid request.
    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        var request = new UpdateSubjectRequest
        {
            SubjectCode = "CODE",
            SubjectName = "Name",
            IsActive = true,
            RowVersion = "1"
        };
        var errors = ValidateModel(request);
        Assert.Empty(errors);
    }

    // 2. SubjectCode null/empty/whitespace.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SubjectCode_NullOrWhitespace_HasError(string? code)
    {
        var request = new UpdateSubjectRequest { SubjectCode = code!, SubjectName = "Name", IsActive = true, RowVersion = "1" };
        var errors = ValidateModel(request);
        Assert.Contains(errors, v => v.MemberNames.Contains(nameof(UpdateSubjectRequest.SubjectCode)));
    }

    // 3. SubjectCode raw length > 32.
    [Fact]
    public void SubjectCode_MaxLength_Exceeded_HasError()
    {
        var request = new UpdateSubjectRequest { SubjectCode = new string('A', 33), SubjectName = "Name", IsActive = true, RowVersion = "1" };
        var errors = ValidateModel(request);
        Assert.Contains(errors, v => v.MemberNames.Contains(nameof(UpdateSubjectRequest.SubjectCode)));
    }

    // 4. SubjectName null/empty/whitespace.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SubjectName_NullOrWhitespace_HasError(string? name)
    {
        var request = new UpdateSubjectRequest { SubjectCode = "CODE", SubjectName = name!, IsActive = true, RowVersion = "1" };
        var errors = ValidateModel(request);
        Assert.Contains(errors, v => v.MemberNames.Contains(nameof(UpdateSubjectRequest.SubjectName)));
    }

    // 5. SubjectName raw length > 100.
    [Fact]
    public void SubjectName_MaxLength_Exceeded_HasError()
    {
        var request = new UpdateSubjectRequest { SubjectCode = "CODE", SubjectName = new string('A', 101), IsActive = true, RowVersion = "1" };
        var errors = ValidateModel(request);
        Assert.Contains(errors, v => v.MemberNames.Contains(nameof(UpdateSubjectRequest.SubjectName)));
    }

    // 6. Description length > 500.
    [Fact]
    public void Description_MaxLength_Exceeded_HasError()
    {
        var request = new UpdateSubjectRequest { SubjectCode = "CODE", SubjectName = "Name", Description = new string('A', 501), IsActive = true, RowVersion = "1" };
        var errors = ValidateModel(request);
        Assert.Contains(errors, v => v.MemberNames.Contains(nameof(UpdateSubjectRequest.Description)));
    }

    // 7. Missing/null IsActive.
    [Fact]
    public void MissingIsActive_HasError()
    {
        var request = new UpdateSubjectRequest { SubjectCode = "CODE", SubjectName = "Name", RowVersion = "1" };
        var errors = ValidateModel(request);
        Assert.Contains(errors, v => v.MemberNames.Contains(nameof(UpdateSubjectRequest.IsActive)));
    }

    // 8. IsActive false hợp lệ.
    [Fact]
    public void IsActiveFalse_IsValid()
    {
        var request = new UpdateSubjectRequest { SubjectCode = "CODE", SubjectName = "Name", IsActive = false, RowVersion = "1" };
        var errors = ValidateModel(request);
        Assert.Empty(errors);
    }

    // 9. RowVersion null/empty/whitespace.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RowVersion_NullOrWhitespace_HasError(string? rowVersion)
    {
        var request = new UpdateSubjectRequest { SubjectCode = "CODE", SubjectName = "Name", IsActive = true, RowVersion = rowVersion! };
        var errors = ValidateModel(request);
        Assert.Contains(errors, v => v.MemberNames.Contains(nameof(UpdateSubjectRequest.RowVersion)));
    }

    // 10. Invalid rowVersion.
    [Theory]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData(" 1 ")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("0")]
    [InlineData("١")] // Unicode digit (Arabic 1)
    [InlineData("18446744073709551616")] // ulong overflow
    public void RowVersion_Invalid_HasError(string rowVersion)
    {
        var request = new UpdateSubjectRequest { SubjectCode = "CODE", SubjectName = "Name", IsActive = true, RowVersion = rowVersion };
        var errors = ValidateModel(request);
        Assert.Contains(errors, v => v.MemberNames.Contains(nameof(UpdateSubjectRequest.RowVersion)));
    }
}
