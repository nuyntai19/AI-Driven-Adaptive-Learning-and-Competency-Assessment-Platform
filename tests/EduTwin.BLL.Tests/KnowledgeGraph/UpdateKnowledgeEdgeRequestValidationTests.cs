using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class UpdateKnowledgeEdgeRequestValidationTests
{
    private static List<ValidationResult> Validate(UpdateKnowledgeEdgeRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Valid_Weight_Boundaries_Accepted(double weightValue)
    {
        var request = new UpdateKnowledgeEdgeRequest
        {
            Weight = (decimal)weightValue,
            RowVersion = "1"
        };

        var results = Validate(request);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("18446744073709551615")]
    public void Valid_RowVersion_Accepted(string rowVersion)
    {
        var request = new UpdateKnowledgeEdgeRequest
        {
            Weight = 0.5m,
            RowVersion = rowVersion
        };

        var results = Validate(request);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Invalid_Weight_Rejected(double? weightValue)
    {
        var request = new UpdateKnowledgeEdgeRequest
        {
            Weight = weightValue.HasValue ? (decimal)weightValue.Value : null,
            RowVersion = "1"
        };

        var results = Validate(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateKnowledgeEdgeRequest.Weight)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData(" 1 ")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("0")]
    [InlineData("18446744073709551616")]
    [InlineData("١")]
    [InlineData("１")]
    [InlineData("abc")]
    public void Invalid_RowVersion_Rejected(string? rowVersion)
    {
        var request = new UpdateKnowledgeEdgeRequest
        {
            Weight = 0.5m,
            RowVersion = rowVersion
        };

        var results = Validate(request);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateKnowledgeEdgeRequest.RowVersion)));
    }
}
