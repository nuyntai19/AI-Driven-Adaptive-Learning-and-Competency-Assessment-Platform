using System.Diagnostics;
using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiAIServiceTests
{
    [Fact]
    public async Task AnalyzeReasoningAsync_ValidProviderJson_ReturnsParserValidatedResponse()
    {
        var client = new FakeGeminiGenerateContentClient();
        var service = CreateService(client);

        var response = await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);

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
    public async Task AnalyzeReasoningAsync_PassesExactRawTextAndOriginalRequestToParser()
    {
        var client = new FakeGeminiGenerateContentClient();
        var parser = new RecordingResponseParser();
        var request = CreateRequest();
        var service = CreateService(client, parser: parser);

        var response = await service.AnalyzeReasoningAsync(request, CancellationToken.None);

        Assert.Equal(1, parser.CallCount);
        Assert.Equal(ValidResponseJson, parser.RawResponse);
        Assert.Same(request, parser.Request);
        Assert.Same(parser.Response, response);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_UsesConfiguredModelAndCallsProviderExactlyOnce()
    {
        var client = new FakeGeminiGenerateContentClient();
        var options = CreateValidOptions();
        options.Model = "configured-model-name";
        var service = CreateService(client, options);

        await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(1, client.CallCount);
        Assert.Equal("configured-model-name", client.Model);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_PassesMinimalPromptAndStructuredJsonConfig()
    {
        var client = new FakeGeminiGenerateContentClient();
        var service = CreateService(client);

        await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);

        Assert.NotNull(client.Prompt);
        Assert.Contains("INPUT_JSON_BEGIN", client.Prompt, StringComparison.Ordinal);
        Assert.Contains("untrusted data", client.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("methodDetected", client.Prompt, StringComparison.Ordinal);
        Assert.NotNull(client.Config?.ResponseJsonSchema);
        using var schemaDocument = JsonDocument.Parse(JsonSerializer.Serialize(client.Config.ResponseJsonSchema));
        Assert.Equal(
            10,
            schemaDocument.RootElement.GetProperty("properties").EnumerateObject().Count());
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_ConfiguresApplicationJsonAndResponseJsonSchemaOnly()
    {
        var client = new FakeGeminiGenerateContentClient();
        var service = CreateService(client);

        await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);

        Assert.NotNull(client.Config);
        Assert.Equal("application/json", client.Config.ResponseMimeType);
        Assert.NotNull(client.Config.ResponseJsonSchema);
        Assert.Null(client.Config.ResponseSchema);
        Assert.Equal(1, client.Config.CandidateCount);
        Assert.Equal(0, client.Config.Temperature);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_PassesCancelableLinkedTokenToProvider()
    {
        using var callerCancellation = new CancellationTokenSource();
        var client = new FakeGeminiGenerateContentClient();
        var service = CreateService(client);

        await service.AnalyzeReasoningAsync(CreateRequest(), callerCancellation.Token);

        Assert.True(client.CancellationToken.CanBeCanceled);
        Assert.NotEqual(callerCancellation.Token, client.CancellationToken);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_PreCanceledCallerToken_DoesNotCallProvider()
    {
        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.Cancel();
        var client = new FakeGeminiGenerateContentClient();
        var parser = new RecordingResponseParser();
        var service = CreateService(client, parser: parser);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), callerCancellation.Token));

        Assert.Equal(callerCancellation.Token, exception.CancellationToken);
        Assert.Equal(0, client.CallCount);
        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_CallerCancellation_PropagatesCancellation()
    {
        using var callerCancellation = new CancellationTokenSource();
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = async (_, _, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ValidResponseJson;
            }
        };
        var parser = new RecordingResponseParser();
        var service = CreateService(client, parser: parser);

        var operation = service.AnalyzeReasoningAsync(CreateRequest(), callerCancellation.Token);
        callerCancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        Assert.Equal(callerCancellation.Token, exception.CancellationToken);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_ProviderDoesNotComplete_CancelsAndThrowsSanitizedTimeout()
    {
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = async (_, _, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return ValidResponseJson;
            }
        };
        var options = CreateValidOptions();
        options.Timeout = TimeSpan.FromMilliseconds(50);
        var parser = new RecordingResponseParser();
        var service = CreateService(client, options, parser);
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        stopwatch.Stop();
        Assert.Equal("AI_PROVIDER_TIMEOUT", exception.ErrorCode);
        Assert.Equal("AI provider request timed out.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(0, parser.CallCount);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_ProviderFailure_ThrowsSanitizedAdapterError()
    {
        const string providerMessage = "RAW_PROVIDER_BODY_AND_SECRET_MUST_NOT_LEAK";
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromException<string>(new InvalidOperationException(providerMessage))
        };
        var parser = new RecordingResponseParser();
        var service = CreateService(client, parser: parser);

        var exception = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_PROVIDER_REQUEST_FAILED", exception.ErrorCode);
        Assert.DoesNotContain(providerMessage, exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_EmptyResponse_ThrowsSanitizedAdapterError()
    {
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult("   ")
        };
        var parser = new RecordingResponseParser();
        var service = CreateService(client, parser: parser);

        var exception = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_PROVIDER_RESPONSE_EMPTY", exception.ErrorCode);
        Assert.Equal("AI provider returned no usable response.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_MalformedJson_ThrowsSanitizedValidationErrorWithoutRawResponse()
    {
        const string malformedResponse = "{\"feedback\":\"RAW_RESPONSE_MUST_NOT_LEAK\"";
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(malformedResponse)
        };
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_JSON_INVALID", exception.ErrorCode);
        Assert.DoesNotContain(malformedResponse, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW_RESPONSE_MUST_NOT_LEAK", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_ExtraField_ThrowsSanitizedValidationError()
    {
        var rawResponse = ValidResponseJson.Replace(
            "\n}",
            ",\n  \"unexpected\": \"RAW_VALUE_MUST_NOT_LEAK\"\n}",
            StringComparison.Ordinal);
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(rawResponse)
        };
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_SHAPE_INVALID", exception.ErrorCode);
        Assert.DoesNotContain("RAW_VALUE_MUST_NOT_LEAK", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_OutOfRangeQuality_ThrowsSanitizedValidationError()
    {
        var rawResponse = ValidResponseJson.Replace(
            "\"reasoningQuality\": 72",
            "\"reasoningQuality\": 101",
            StringComparison.Ordinal);
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(rawResponse)
        };
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_SEMANTIC_INVALID", exception.ErrorCode);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_LanguageMismatch_ThrowsSanitizedValidationError()
    {
        var rawResponse = ValidResponseJson.Replace(
            "\"language\": \"vi\"",
            "\"language\": \"en\"",
            StringComparison.Ordinal);
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(rawResponse)
        };
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_SEMANTIC_INVALID", exception.ErrorCode);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_HallucinatedRootNode_ThrowsSanitizedValidationError()
    {
        var rawResponse = ValidResponseJson.Replace(
            "\"rootCauseNodeIds\": [\"101\"]",
            "\"rootCauseNodeIds\": [\"999\"]",
            StringComparison.Ordinal);
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(rawResponse)
        };
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_SEMANTIC_INVALID", exception.ErrorCode);
        Assert.DoesNotContain("999", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_ValidationFailure_DoesNotRetryProvider()
    {
        var rawResponse = ValidResponseJson.Replace(
            "\"confidence\": 85",
            "\"confidence\": 101",
            StringComparison.Ordinal);
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(rawResponse)
        };
        var service = CreateService(client);

        await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_DoesNotRetryAfterFailure()
    {
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromException<string>(new InvalidOperationException("failure"))
        };
        var service = CreateService(client);

        await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_InvalidConfiguration_FailsBeforeProviderCallWithoutLeakingValues()
    {
        const string secret = "CONFIGURED_API_KEY_MUST_NOT_LEAK";
        var client = new FakeGeminiGenerateContentClient();
        var options = CreateValidOptions();
        options.ApiKey = secret;
        options.Model = "   ";
        var parser = new RecordingResponseParser();
        var service = CreateService(client, options, parser);

        var exception = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_PROVIDER_CONFIGURATION_INVALID", exception.ErrorCode);
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        Assert.Equal(0, client.CallCount);
        Assert.Equal(0, parser.CallCount);
    }

    private static GeminiAIService CreateService(
        FakeGeminiGenerateContentClient client,
        GeminiOptions? options = null,
        IAIAnalysisResponseParser? parser = null) =>
        new(
            Options.Create(options ?? CreateValidOptions()),
            client,
            new GeminiPromptBuilder(),
            new GeminiResponseJsonSchema(),
            parser ?? new StrictAIAnalysisResponseParser(new AnalyzeReasoningResponseValidator()));

    private static GeminiOptions CreateValidOptions() =>
        new()
        {
            ApiKey = "unit-test-api-key",
            Model = "unit-test-model",
            Timeout = TimeSpan.FromSeconds(30)
        };

    private static AnalyzeReasoningRequest CreateRequest() =>
        new()
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = "vi",
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

    private sealed class FakeGeminiGenerateContentClient : IGeminiGenerateContentClient
    {
        public Func<string, string, GenerateContentConfig, CancellationToken, Task<string>> Handler { get; init; } =
            (_, _, _, _) => Task.FromResult(ValidResponseJson);

        public int CallCount { get; private set; }

        public string? Model { get; private set; }

        public string? Prompt { get; private set; }

        public GenerateContentConfig? Config { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<string> GenerateContentAsync(
            string model,
            string prompt,
            GenerateContentConfig config,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Model = model;
            Prompt = prompt;
            Config = config;
            CancellationToken = cancellationToken;
            return Handler(model, prompt, config, cancellationToken);
        }
    }

    private sealed class RecordingResponseParser : IAIAnalysisResponseParser
    {
        public int CallCount { get; private set; }

        public string? RawResponse { get; private set; }

        public AnalyzeReasoningRequest? Request { get; private set; }

        public AnalyzeReasoningResponse Response { get; } = new()
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = "vi",
            MethodDetected = null,
            ReasoningQuality = 50,
            ErrorType = ErrorType.None,
            Misconception = null,
            MissingSteps = [],
            RootCauseNodeIds = [],
            Confidence = 50,
            Feedback = "Validated response"
        };

        public AnalyzeReasoningResponse ParseAndValidate(
            string rawResponse,
            AnalyzeReasoningRequest request)
        {
            CallCount++;
            RawResponse = rawResponse;
            Request = request;
            return Response;
        }
    }
}
