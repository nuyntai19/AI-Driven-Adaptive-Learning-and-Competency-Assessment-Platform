using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AttemptSummaries;

public sealed class AttemptSummaryContractTests
{
    [Fact]
    public void Serialization_ContainsOnlyFrozenSection55Fields()
    {
        var response = new AttemptListResponse
        {
            Data =
            [
                new AttemptSummaryDto
                {
                    AttemptId = "18446744073709551615",
                    StudentId = "baf68743-a272-4983-a9e2-41663734a7c2",
                    StudentName = "Trần Minh An",
                    SubjectId = "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
                    QuestionId = "9007199254740993",
                    QuestionText = "Giải phương trình ...",
                    AssignmentId = null,
                    AttemptStatus = "PendingAnalysis",
                    Grading = new AttemptSummaryGradingDto
                    {
                        IsCorrect = null,
                        AwardedScore = null,
                        MaxScore = 1.5m,
                        Skipped = false
                    },
                    AnalysisJobId = "9007199254740994",
                    JobStatus = "Pending",
                    Terminal = false,
                    PollUrl = "/api/v1/learning/analysis-jobs/9007199254740994",
                    CreatedAt = new DateTime(2026, 7, 15, 8, 30, 45, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 7, 15, 8, 31, 6, DateTimeKind.Utc)
                }
            ],
            Meta = new PagedMetaDto
            {
                Page = 1,
                PageSize = 20,
                TotalItems = 1,
                TotalPages = 1,
                TraceId = "trace-id",
                Timestamp = new DateTime(2026, 7, 15, 8, 31, 6, DateTimeKind.Utc)
            }
        };

        var json = JsonSerializer.SerializeToElement(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(new[] { "data", "meta" }, Names(json));
        var item = json.GetProperty("data")[0];
        Assert.Equal(
            new[]
            {
                "attemptId", "studentId", "studentName", "subjectId", "questionId",
                "questionText", "assignmentId", "attemptStatus", "grading",
                "analysisJobId", "jobStatus", "terminal", "pollUrl", "createdAt", "updatedAt"
            },
            Names(item));
        Assert.Equal(
            new[] { "isCorrect", "awardedScore", "maxScore", "skipped" },
            Names(item.GetProperty("grading")));
        Assert.Equal(
            new[] { "page", "pageSize", "totalItems", "totalPages", "traceId", "timestamp" },
            Names(json.GetProperty("meta")));
        Assert.Equal(JsonValueKind.String, item.GetProperty("attemptId").ValueKind);
        Assert.Equal(JsonValueKind.String, item.GetProperty("questionId").ValueKind);
        Assert.Equal(JsonValueKind.String, item.GetProperty("analysisJobId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("assignmentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("grading").GetProperty("isCorrect").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("grading").GetProperty("awardedScore").ValueKind);

        var serialized = item.GetRawText();
        foreach (var forbidden in new[]
                 {
                     "finalAnswer", "reasoningText", "correctAnswer", "solution",
                     "expectedReasoning", "gradingCriteria", "analysis", "feedback",
                     "feedbackUrl", "twin", "recommendation", "clientSubmissionId",
                     "centerId", "rowVersion", "createdBy", "updatedBy", "retryCount",
                     "leaseOwner", "leaseUntil", "lastErrorCode", "lastErrorMessage",
                     "correlationId", "provider", "rawPayload"
                 })
        {
            Assert.DoesNotContain($"\"{forbidden}\"", serialized, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void QueryContract_HasExactNineRawPropertiesAndPreservesEmptyStrings()
    {
        var properties = typeof(ListAttemptsQuery).GetProperties();
        Assert.Equal(
            new[]
            {
                "StudentId", "SubjectId", "QuestionId", "AssignmentId", "Status",
                "From", "To", "Page", "PageSize"
            },
            properties.Select(property => property.Name).ToArray());
        Assert.All(properties, property => Assert.Equal(typeof(string), property.PropertyType));
        Assert.All(properties, property =>
        {
            var display = Assert.Single(
                property.GetCustomAttributes(typeof(DisplayFormatAttribute), inherit: true)
                    .Cast<DisplayFormatAttribute>());
            Assert.False(display.ConvertEmptyStringToNull);
        });
    }

    private static string[] Names(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).ToArray();
}
