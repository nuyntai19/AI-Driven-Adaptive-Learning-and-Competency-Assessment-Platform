using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Tests.DigitalTwin;

public class UpsertStudentSubjectGoalRequestValidationTests
{
    private IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    [Theory]
    [InlineData(8.5, 120, null, true)] // valid create
    [InlineData(8.5, 120, "1", true)] // valid update
    [InlineData(0, 0, null, true)] // boundary min
    [InlineData(10, 3650, null, true)] // boundary max
    public void Request_ValidScenarios_PassesValidation(decimal score, int days, string? version, bool isValid)
    {
        var request = new UpsertStudentSubjectGoalRequest
        {
            TargetScore = score,
            RemainingDays = days,
            RowVersion = version
        };
        var results = ValidateModel(request);
        Assert.Equal(isValid, !results.Any());
    }

    [Fact]
    public void Request_TargetScore_BelowZero_Fails()
    {
        var request = new UpsertStudentSubjectGoalRequest { TargetScore = -0.1m, RemainingDays = 120 };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains("TargetScore"));
    }

    [Fact]
    public void Request_TargetScore_AboveTen_Fails()
    {
        var request = new UpsertStudentSubjectGoalRequest { TargetScore = 10.1m, RemainingDays = 120 };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains("TargetScore"));
    }

    [Fact]
    public void Request_TargetScore_TooManyDecimalPlaces_Fails()
    {
        var request = new UpsertStudentSubjectGoalRequest { TargetScore = 8.555m, RemainingDays = 120 };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains("TargetScore") && r.ErrorMessage!.Contains("decimal places"));
    }

    [Fact]
    public void Request_TargetScore_TrailingZeros_Fails()
    {
        var request = new UpsertStudentSubjectGoalRequest { TargetScore = 8.500m, RemainingDays = 120 };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains("TargetScore") && r.ErrorMessage!.Contains("decimal places"));
    }

    [Fact]
    public void Request_RemainingDays_BelowZero_Fails()
    {
        var request = new UpsertStudentSubjectGoalRequest { TargetScore = 8.5m, RemainingDays = -1 };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains("RemainingDays"));
    }

    [Fact]
    public void Request_RemainingDays_AboveMax_Fails()
    {
        var request = new UpsertStudentSubjectGoalRequest { TargetScore = 8.5m, RemainingDays = 3651 };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains("RemainingDays"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 123")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("18446744073709551616")] // ulong.MaxValue + 1
    public void Request_RowVersion_Invalid_Fails(string invalidVersion)
    {
        var request = new UpsertStudentSubjectGoalRequest { TargetScore = 8.5m, RemainingDays = 120, RowVersion = invalidVersion };
        var results = ValidateModel(request);
        Assert.Contains(results, r => r.MemberNames.Contains("RowVersion"));
    }
}
