using System.Text.Json.Nodes;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class StrictAIAnalysisResponseParserTests
{
    [Fact]
    public void ParseAndValidate_ValidVietnameseJson_ReturnsExactTypedResponse()
    {
        var response = CreateParser().ParseAndValidate(ValidResponseJson, CreateRequest());

        Assert.Equal(AIAnalysisContract.SchemaVersion, response.SchemaVersion);
        Assert.Equal("vi", response.Language);
        Assert.Equal("Đưa hai vế về cùng cơ số", response.MethodDetected);
        Assert.Equal(72, response.ReasoningQuality);
        Assert.Equal(ErrorType.Reasoning, response.ErrorType);
        Assert.Null(response.Misconception);
        Assert.Equal(["Chưa đối chiếu điều kiện"], response.MissingSteps);
        Assert.Equal(["101"], response.RootCauseNodeIds);
        Assert.Equal(85, response.Confidence);
        Assert.Equal("Em đã chọn đúng phương pháp.", response.Feedback);
    }

    [Fact]
    public void ParseAndValidate_ValidEnglishJson_ReturnsExactTypedResponse()
    {
        var request = CreateRequest(language: "en");
        var rawResponse = ValidResponseJson
            .Replace("\"language\": \"vi\"", "\"language\": \"en\"", StringComparison.Ordinal)
            .Replace("Em đã chọn đúng phương pháp.", "Your method is correct.", StringComparison.Ordinal);

        var response = CreateParser().ParseAndValidate(rawResponse, request);

        Assert.Equal("en", response.Language);
        Assert.Equal("Your method is correct.", response.Feedback);
    }

    [Fact]
    public void ParseAndValidate_NullNullableFields_AcceptsNull()
    {
        var rawResponse = ReplacePropertyValue(ValidResponseJson, "methodDetected", "null");
        rawResponse = ReplacePropertyValue(rawResponse, "misconception", "null");

        var response = CreateParser().ParseAndValidate(rawResponse, CreateRequest());

        Assert.Null(response.MethodDetected);
        Assert.Null(response.Misconception);
    }

    [Fact]
    public void ParseAndValidate_EmptyArrays_AcceptsEmptyCollections()
    {
        var rawResponse = ReplacePropertyValue(ValidResponseJson, "missingSteps", "[]");
        rawResponse = ReplacePropertyValue(rawResponse, "rootCauseNodeIds", "[]");

        var response = CreateParser().ParseAndValidate(rawResponse, CreateRequest());

        Assert.Empty(response.MissingSteps);
        Assert.Empty(response.RootCauseNodeIds);
    }

    [Fact]
    public void ParseAndValidate_ValidResponse_PassesOriginalRequestAndResponseToValidatorExactlyOnce()
    {
        var validator = new RecordingValidator();
        var parser = new StrictAIAnalysisResponseParser(validator);
        var request = CreateRequest();

        var response = parser.ParseAndValidate(ValidResponseJson, request);

        Assert.Equal(1, validator.CallCount);
        Assert.Same(request, validator.Request);
        Assert.Same(response, validator.Response);
    }

    [Fact]
    public void ParseAndValidate_NullRawResponse_ThrowsSanitizedJsonError()
    {
        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(null!, CreateRequest()));

        AssertJsonInvalid(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void ParseAndValidate_EmptyOrWhitespaceRawResponse_ThrowsSanitizedJsonError(string rawResponse)
    {
        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));

        AssertJsonInvalid(exception);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":")]
    [InlineData("{not-json}")]
    public void ParseAndValidate_MalformedJson_ThrowsSanitizedJsonError(string rawResponse)
    {
        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));

        AssertJsonInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_MultipleRootValues_ThrowsSanitizedJsonError()
    {
        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate($"{ValidResponseJson}\n{{}}", CreateRequest()));

        AssertJsonInvalid(exception);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void ParseAndValidate_NonObjectRoot_ThrowsSanitizedShapeError(string rawResponse)
    {
        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));

        AssertShapeInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_Comment_ThrowsSanitizedJsonError()
    {
        var rawResponse = ValidResponseJson.Replace("{", "{\n// untrusted comment", StringComparison.Ordinal);

        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));

        AssertJsonInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_TrailingComma_ThrowsSanitizedJsonError()
    {
        var rawResponse = ValidResponseJson.Replace("\n}", ",\n}", StringComparison.Ordinal);

        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));

        AssertJsonInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_MalformedJson_DoesNotLeakRawSentinelOrInnerException()
    {
        const string sentinel = "RAW_AI_RESPONSE_MUST_NOT_LEAK";
        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate($"{{\"feedback\":\"{sentinel}\"", CreateRequest()));

        AssertJsonInvalid(exception);
        Assert.DoesNotContain(sentinel, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("language")]
    [InlineData("methodDetected")]
    [InlineData("reasoningQuality")]
    [InlineData("errorType")]
    [InlineData("misconception")]
    [InlineData("missingSteps")]
    [InlineData("rootCauseNodeIds")]
    [InlineData("confidence")]
    [InlineData("feedback")]
    public void ParseAndValidate_MissingCanonicalProperty_ThrowsSanitizedShapeError(string propertyName)
    {
        var json = JsonNode.Parse(ValidResponseJson)!.AsObject();
        Assert.True(json.Remove(propertyName));

        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(json.ToJsonString(), CreateRequest()));

        AssertShapeInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_UnknownProperty_ThrowsSanitizedShapeError()
    {
        var json = JsonNode.Parse(ValidResponseJson)!.AsObject();
        json["unexpected"] = true;

        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(json.ToJsonString(), CreateRequest()));

        AssertShapeInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_DuplicateCanonicalProperty_ThrowsSanitizedShapeError()
    {
        var rawResponse = ValidResponseJson.Replace(
            "\"schemaVersion\": \"ai-analysis-v1\",",
            "\"schemaVersion\": \"ai-analysis-v1\",\n  \"schemaVersion\": \"ai-analysis-v1\",",
            StringComparison.Ordinal);

        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));

        AssertShapeInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_WrongCaseProperty_ThrowsSanitizedShapeError()
    {
        var rawResponse = ValidResponseJson.Replace("\"feedback\"", "\"Feedback\"", StringComparison.Ordinal);

        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));

        AssertShapeInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_SnakeCaseAlias_ThrowsSanitizedShapeError()
    {
        var rawResponse = ValidResponseJson.Replace(
            "\"reasoningQuality\"",
            "\"reasoning_quality\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));

        AssertShapeInvalid(exception);
    }

    [Fact]
    public void ParseAndValidate_ExactlyTenCanonicalFields_InvokesValidator()
    {
        var validator = new RecordingValidator();
        var parser = new StrictAIAnalysisResponseParser(validator);

        parser.ParseAndValidate(ValidResponseJson, CreateRequest());

        Assert.Equal(1, validator.CallCount);
    }

    [Theory]
    [InlineData("schemaVersion", "null")]
    [InlineData("schemaVersion", "42")]
    [InlineData("language", "null")]
    [InlineData("language", "true")]
    [InlineData("feedback", "null")]
    [InlineData("feedback", "[]")]
    public void ParseAndValidate_RequiredStringNullOrWrongType_ThrowsSanitizedShapeError(
        string propertyName,
        string rawValue)
    {
        AssertShapeFailure(ReplacePropertyValue(ValidResponseJson, propertyName, rawValue));
    }

    [Theory]
    [InlineData("methodDetected", "42")]
    [InlineData("methodDetected", "{}")]
    [InlineData("methodDetected", "[]")]
    [InlineData("misconception", "42")]
    [InlineData("misconception", "{}")]
    [InlineData("misconception", "[]")]
    public void ParseAndValidate_NullableTextWrongType_ThrowsSanitizedShapeError(
        string propertyName,
        string rawValue)
    {
        AssertShapeFailure(ReplacePropertyValue(ValidResponseJson, propertyName, rawValue));
    }

    [Theory]
    [InlineData("reasoningQuality", "\"72\"")]
    [InlineData("reasoningQuality", "72.0")]
    [InlineData("reasoningQuality", "72e0")]
    [InlineData("reasoningQuality", "-0")]
    [InlineData("reasoningQuality", "true")]
    [InlineData("reasoningQuality", "null")]
    [InlineData("confidence", "\"85\"")]
    [InlineData("confidence", "85.0")]
    [InlineData("confidence", "85E0")]
    [InlineData("confidence", "-0")]
    [InlineData("confidence", "false")]
    [InlineData("confidence", "null")]
    public void ParseAndValidate_PercentageWrongOrNonIntegralLexicalType_ThrowsSanitizedShapeError(
        string propertyName,
        string rawValue)
    {
        AssertShapeFailure(ReplacePropertyValue(ValidResponseJson, propertyName, rawValue));
    }

    [Theory]
    [InlineData("3")]
    [InlineData("\"3\"")]
    [InlineData("\"reasoning\"")]
    [InlineData("\"NotDefined\"")]
    public void ParseAndValidate_ErrorTypeInvalidRepresentation_ThrowsSanitizedShapeError(string rawValue)
    {
        AssertShapeFailure(ReplacePropertyValue(ValidResponseJson, "errorType", rawValue));
    }

    [Theory]
    [InlineData("missingSteps", "null")]
    [InlineData("missingSteps", "\"step\"")]
    [InlineData("missingSteps", "[\"step\", 1]")]
    [InlineData("missingSteps", "[null]")]
    [InlineData("rootCauseNodeIds", "null")]
    [InlineData("rootCauseNodeIds", "\"101\"")]
    [InlineData("rootCauseNodeIds", "[\"101\", true]")]
    [InlineData("rootCauseNodeIds", "[null]")]
    public void ParseAndValidate_ArrayInvalidShapeOrItem_ThrowsSanitizedShapeError(
        string propertyName,
        string rawValue)
    {
        AssertShapeFailure(ReplacePropertyValue(ValidResponseJson, propertyName, rawValue));
    }

    private static StrictAIAnalysisResponseParser CreateParser() =>
        new(new AnalyzeReasoningResponseValidator());

    private static void AssertShapeFailure(string rawResponse)
    {
        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => CreateParser().ParseAndValidate(rawResponse, CreateRequest()));
        AssertShapeInvalid(exception);
    }

    private static void AssertJsonInvalid(AIAnalysisValidationException exception)
    {
        Assert.Equal("AI_RESPONSE_JSON_INVALID", exception.ErrorCode);
        Assert.Equal("AI response JSON is invalid.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    private static void AssertShapeInvalid(AIAnalysisValidationException exception)
    {
        Assert.Equal("AI_RESPONSE_SHAPE_INVALID", exception.ErrorCode);
        Assert.Equal("AI response shape is invalid.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    private static string ReplacePropertyValue(string rawResponse, string propertyName, string rawValue)
    {
        var json = JsonNode.Parse(rawResponse)!.AsObject();
        json[propertyName] = rawValue == "null" ? null : JsonNode.Parse(rawValue);
        return json.ToJsonString();
    }

    private static AnalyzeReasoningRequest CreateRequest(string language = "vi") =>
        new()
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = language,
            Question = new AnalyzeReasoningQuestion
            {
                QuestionType = QuestionType.MultipleChoice,
                QuestionText = "Giải phương trình ...",
                CorrectAnswer = "B",
                Solution = "Lời giải chuẩn",
                ExpectedReasoning = "Xác định điều kiện.",
                GradingCriteria = new AnalyzeReasoningGradingCriteria
                {
                    SchemaVersion = "1.0",
                    RequiredIdeas = ["Xác định điều kiện"],
                    CommonErrors = ["Quên điều kiện"],
                    ScoringNotes = "Ưu tiên reasoning"
                }
            },
            StudentSubmission = new AnalyzeReasoningStudentSubmission
            {
                FinalAnswer = "B",
                ReasoningText = "Em đặt điều kiện rồi biến đổi.",
                TimeSpentSeconds = 165,
                Confidence = 80,
                AnswerChanges = 1
            },
            AllowedKnowledgeNodes =
            [
                new AnalyzeReasoningAllowedKnowledgeNode
                {
                    NodeId = "101",
                    NodeName = "Mũ và Logarit"
                }
            ]
        };

    private const string ValidResponseJson = """
        {
          "schemaVersion": "ai-analysis-v1",
          "language": "vi",
          "methodDetected": "Đưa hai vế về cùng cơ số",
          "reasoningQuality": 72,
          "errorType": "Reasoning",
          "misconception": null,
          "missingSteps": ["Chưa đối chiếu điều kiện"],
          "rootCauseNodeIds": ["101"],
          "confidence": 85,
          "feedback": "Em đã chọn đúng phương pháp."
        }
        """;

    private sealed class RecordingValidator : IAnalyzeReasoningResponseValidator
    {
        public int CallCount { get; private set; }

        public AnalyzeReasoningRequest? Request { get; private set; }

        public AnalyzeReasoningResponse? Response { get; private set; }

        public void Validate(AnalyzeReasoningRequest request, AnalyzeReasoningResponse response)
        {
            CallCount++;
            Request = request;
            Response = response;
        }
    }
}
