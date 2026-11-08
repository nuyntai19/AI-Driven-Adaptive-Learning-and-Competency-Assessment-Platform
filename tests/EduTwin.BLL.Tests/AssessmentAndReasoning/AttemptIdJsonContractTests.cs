using System.Text.Json;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Recommendations;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning;

/// <summary>
/// Attempt ids can exceed 2^53, which a JavaScript number silently rounds, so
/// the API must send them as strings.
/// </summary>
public sealed class AttemptIdJsonContractTests
{
    private const ulong AboveJavaScriptSafeInteger = 9031259002542264713;
    private const string Exact = "9031259002542264713";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static JsonElement Serialize<T>(T value) => JsonSerializer.SerializeToElement(value, Web);

    [Fact]
    public void StudentQuestion_SendsSubmittedAttemptIdAsString() =>
        Assert.Equal(Exact, Serialize(new StudentQuestionDto { SubmittedAttemptId = AboveJavaScriptSafeInteger })
            .GetProperty("submittedAttemptId").GetString());

    [Fact]
    public void StudentQuestion_KeepsMissingAttemptNull() =>
        Assert.Equal(JsonValueKind.Null, Serialize(new StudentQuestionDto()).GetProperty("submittedAttemptId").ValueKind);

    [Fact]
    public void StudentReviewRequest_SendsAttemptIdAsString() =>
        Assert.Equal(Exact, Serialize(new StudentReviewRequestDto { AttemptId = AboveJavaScriptSafeInteger })
            .GetProperty("attemptId").GetString());

    [Fact]
    public void ActivitySubmission_SendsAttemptIdAsString() =>
        Assert.Equal(Exact, Serialize(new ActivitySubmissionResponseDto { AttemptId = AboveJavaScriptSafeInteger })
            .GetProperty("attemptId").GetString());
}
