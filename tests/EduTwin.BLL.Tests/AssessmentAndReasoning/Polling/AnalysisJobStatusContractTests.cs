using System.Text.Json;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Polling;

public sealed class AnalysisJobStatusContractTests
{
    [Fact]
    public void ResponseSerialization_ContainsOnlyFrozenSection53Fields()
    {
        var response = new AnalysisJobStatusResponse
        {
            Data = new AnalysisJobStatusDataDto
            {
                AnalysisJobId = "13001",
                AttemptId = "12001",
                Status = "Processing",
                RetryCount = 0,
                Terminal = false,
                FeedbackUrl = null,
                UpdatedAt = new DateTime(
                    2026,
                    7,
                    15,
                    8,
                    31,
                    0,
                    DateTimeKind.Utc)
            },
            Meta = new MetaDto
            {
                TraceId = "trace-id",
                Timestamp = new DateTime(
                    2026,
                    7,
                    15,
                    8,
                    31,
                    1,
                    DateTimeKind.Utc)
            }
        };

        var json = JsonSerializer.SerializeToElement(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            new[] { "data", "meta" },
            json.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(
            new[]
            {
                "analysisJobId",
                "attemptId",
                "status",
                "retryCount",
                "terminal",
                "feedbackUrl",
                "updatedAt"
            },
            json.GetProperty("data")
                .EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.DoesNotContain(
            json.GetProperty("data").EnumerateObject(),
            property => property.Name is "leaseOwner"
                or "leaseUntil"
                or "lastErrorCode"
                or "lastErrorMessage"
                or "rowVersion"
                or "correlationId"
                or "finalAnswer"
                or "reasoningText");
        Assert.Equal(JsonValueKind.String, json.GetProperty("data").GetProperty("analysisJobId").ValueKind);
        Assert.Equal(JsonValueKind.String, json.GetProperty("data").GetProperty("attemptId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("data").GetProperty("feedbackUrl").ValueKind);
    }
}
