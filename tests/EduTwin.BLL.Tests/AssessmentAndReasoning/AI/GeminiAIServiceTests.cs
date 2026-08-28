using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.API.AssessmentAndReasoning.Background;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
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
    public async Task AnalyzeReasoningAsync_Success_LogsOneTerminalEventWithTokensAndDeterministicLatency()
    {
        var logs = new CapturingLoggerProvider();
        var timeProvider = new ManualTimeProvider();
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(125));
                return Task.FromResult(ProviderResult(
                    promptTokenCount: 11,
                    candidatesTokenCount: 22,
                    totalTokenCount: 33));
            }
        };
        var service = CreateService(
            client,
            logProvider: logs,
            timeProvider: timeProvider);

        await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);

        AssertTerminalEvent(
            logs,
            LogLevel.Information,
            "Succeeded",
            null,
            "unit-test-model",
            125d,
            11,
            22,
            33);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_MissingTokenMetadata_LogsNullableTokensWithoutEstimation()
    {
        var logs = new CapturingLoggerProvider();
        var service = CreateService(
            new FakeGeminiGenerateContentClient(),
            logProvider: logs);

        await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);

        AssertTerminalEvent(
            logs,
            LogLevel.Information,
            "Succeeded",
            null,
            "unit-test-model",
            0d,
            null,
            null,
            null);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_NegativeTokenMetadata_NormalizesEachValueToNull()
    {
        var logs = new CapturingLoggerProvider();
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult(
                promptTokenCount: -1,
                candidatesTokenCount: -2,
                totalTokenCount: -3))
        };
        var service = CreateService(client, logProvider: logs);

        await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);

        AssertTerminalEvent(
            logs,
            LogLevel.Information,
            "Succeeded",
            null,
            "unit-test-model",
            0d,
            null,
            null,
            null);
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
        var logs = new CapturingLoggerProvider();
        var timeProvider = new ManualTimeProvider();
        var service = CreateService(
            client,
            parser: parser,
            logProvider: logs,
            timeProvider: timeProvider);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), callerCancellation.Token));

        Assert.Equal(callerCancellation.Token, exception.CancellationToken);
        Assert.Equal(0, client.CallCount);
        Assert.Equal(0, parser.CallCount);
        Assert.Empty(logs.Entries);
        Assert.Equal(0, timeProvider.TimestampReadCount);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_CallerCancellation_PropagatesCancellation()
    {
        using var callerCancellation = new CancellationTokenSource();
        var logs = new CapturingLoggerProvider();
        var timeProvider = new ManualTimeProvider();
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, cancellationToken) =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(75));
                callerCancellation.Cancel();
                return Task.FromCanceled<GeminiGenerateContentResult>(cancellationToken);
            }
        };
        var parser = new RecordingResponseParser();
        var service = CreateService(
            client,
            parser: parser,
            logProvider: logs,
            timeProvider: timeProvider);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), callerCancellation.Token));

        Assert.Equal(callerCancellation.Token, exception.CancellationToken);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(0, parser.CallCount);
        AssertTerminalEvent(
            logs,
            LogLevel.Information,
            "Canceled",
            "AI_PROVIDER_CALL_CANCELED",
            "unit-test-model",
            75d,
            null,
            null,
            null);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_ProviderDoesNotComplete_CancelsAndThrowsSanitizedTimeout()
    {
        var logs = new CapturingLoggerProvider();
        var timeProvider = new ManualTimeProvider();
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = async (_, _, _, cancellationToken) =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(50));
                var completion = new TaskCompletionSource<GeminiGenerateContentResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = cancellationToken.Register(
                    () => completion.TrySetCanceled(cancellationToken));
                return await completion.Task;
            }
        };
        var options = CreateValidOptions();
        options.Timeout = TimeSpan.FromMilliseconds(50);
        var parser = new RecordingResponseParser();
        var service = CreateService(
            client,
            options,
            parser,
            logs,
            timeProvider);
        var exception = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_PROVIDER_TIMEOUT", exception.ErrorCode);
        Assert.Equal("AI provider request timed out.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(0, parser.CallCount);
        AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_PROVIDER_TIMEOUT",
            "unit-test-model",
            50d,
            null,
            null,
            null);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_ProviderFailure_ThrowsSanitizedAdapterError()
    {
        const string providerMessage = "RAW_PROVIDER_BODY_AND_SECRET_MUST_NOT_LEAK";
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromException<GeminiGenerateContentResult>(
                new InvalidOperationException(providerMessage))
        };
        var parser = new RecordingResponseParser();
        var logs = new CapturingLoggerProvider();
        var service = CreateService(client, parser: parser, logProvider: logs);

        var exception = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_PROVIDER_REQUEST_FAILED", exception.ErrorCode);
        Assert.DoesNotContain(providerMessage, exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        Assert.Equal(0, parser.CallCount);
        var terminal = AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_PROVIDER_REQUEST_FAILED",
            "unit-test-model",
            0d,
            null,
            null,
            null);
        Assert.DoesNotContain(providerMessage, Flatten(terminal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_EmptyResponse_ThrowsSanitizedAdapterError()
    {
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult("   ", 4, 5, 9))
        };
        var parser = new RecordingResponseParser();
        var logs = new CapturingLoggerProvider();
        var service = CreateService(client, parser: parser, logProvider: logs);

        var exception = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_PROVIDER_RESPONSE_EMPTY", exception.ErrorCode);
        Assert.Equal("AI provider returned no usable response.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Equal(0, parser.CallCount);
        AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_PROVIDER_RESPONSE_EMPTY",
            "unit-test-model",
            0d,
            4,
            5,
            9);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_MalformedJson_ThrowsSanitizedValidationErrorWithoutRawResponse()
    {
        const string malformedResponse = "{\"feedback\":\"RAW_RESPONSE_MUST_NOT_LEAK\"";
        var client = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult(malformedResponse, 6, 7, 13))
        };
        var logs = new CapturingLoggerProvider();
        var service = CreateService(client, logProvider: logs);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_JSON_INVALID", exception.ErrorCode);
        Assert.DoesNotContain(malformedResponse, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW_RESPONSE_MUST_NOT_LEAK", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        var terminal = AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_RESPONSE_JSON_INVALID",
            "unit-test-model",
            0d,
            6,
            7,
            13);
        Assert.DoesNotContain("RAW_RESPONSE_MUST_NOT_LEAK", Flatten(terminal), StringComparison.Ordinal);
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
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult(rawResponse, 8, 9, 17))
        };
        var logs = new CapturingLoggerProvider();
        var service = CreateService(client, logProvider: logs);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_SHAPE_INVALID", exception.ErrorCode);
        Assert.DoesNotContain("RAW_VALUE_MUST_NOT_LEAK", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_RESPONSE_SHAPE_INVALID",
            "unit-test-model",
            0d,
            8,
            9,
            17);
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
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult(rawResponse, 10, 11, 21))
        };
        var logs = new CapturingLoggerProvider();
        var service = CreateService(client, logProvider: logs);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_SEMANTIC_INVALID", exception.ErrorCode);
        Assert.Null(exception.InnerException);
        AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_RESPONSE_SEMANTIC_INVALID",
            "unit-test-model",
            0d,
            10,
            11,
            21);
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
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult(rawResponse))
        };
        var logs = new CapturingLoggerProvider();
        var service = CreateService(client, logProvider: logs);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_SEMANTIC_INVALID", exception.ErrorCode);
        Assert.Null(exception.InnerException);
        AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_RESPONSE_SEMANTIC_INVALID",
            "unit-test-model",
            0d,
            null,
            null,
            null);
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
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult(rawResponse))
        };
        var logs = new CapturingLoggerProvider();
        var service = CreateService(client, logProvider: logs);

        var exception = await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_RESPONSE_SEMANTIC_INVALID", exception.ErrorCode);
        Assert.DoesNotContain("999", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_RESPONSE_SEMANTIC_INVALID",
            "unit-test-model",
            0d,
            null,
            null,
            null);
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
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult(rawResponse))
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
            Handler = (_, _, _, _) => Task.FromException<GeminiGenerateContentResult>(
                new InvalidOperationException("failure"))
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
        var logs = new CapturingLoggerProvider();
        var service = CreateService(client, options, parser, logs);

        var exception = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("AI_PROVIDER_CONFIGURATION_INVALID", exception.ErrorCode);
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
        Assert.Equal(0, client.CallCount);
        Assert.Equal(0, parser.CallCount);
        var terminal = AssertTerminalEvent(
            logs,
            LogLevel.Warning,
            "Failed",
            "AI_PROVIDER_CONFIGURATION_INVALID",
            null,
            0d,
            null,
            null,
            null);
        Assert.DoesNotContain(secret, Flatten(terminal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_BackgroundCategoryScope_PropagatesAcrossGeminiCategoryAndDoesNotLeak()
    {
        var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(logs);
        });
        var backgroundLogger = loggerFactory.CreateLogger(
            typeof(AIAnalysisJobBackgroundService).FullName!);
        var geminiLogger = loggerFactory.CreateLogger<GeminiAIService>();
        var service = CreateService(
            new FakeGeminiGenerateContentClient(),
            logProvider: logs,
            logger: geminiLogger);
        var identity = new Dictionary<string, object?>
        {
            ["CorrelationId"] = "correlation-scope",
            ["CenterId"] = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            ["AttemptId"] = 12001ul,
            ["AnalysisJobId"] = 13001ul,
            ["WorkerId"] = "worker-scope"
        };

        using (backgroundLogger.BeginScope(identity))
        {
            await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);
        }

        await service.AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None);

        var terminalEntries = logs.Entries
            .Where(entry => entry.State.ContainsKey("Provider"))
            .ToArray();
        Assert.Equal(2, terminalEntries.Length);
        var inheritedScope = Assert.Single(terminalEntries[0].Scopes);
        Assert.Equal(identity, inheritedScope);
        Assert.Empty(terminalEntries[1].Scopes);
    }

    [Fact]
    public async Task AnalyzeReasoningAsync_TerminalLogs_RedactSecretsPayloadsAndProviderDiagnostics()
    {
        const string apiKey = "API_KEY_SENTINEL_7F2A";
        const string jwt = "JWT_SENTINEL_8B3C";
        const string password = "PASSWORD_SENTINEL_9C4D";
        const string question = "QUESTION_SENTINEL_A5E6";
        const string answer = "ANSWER_SENTINEL_B6F7";
        const string reasoning = "REASONING_SENTINEL_C7A8";
        const string feedback = "FEEDBACK_SENTINEL_D8B9";
        const string providerDiagnostic = "PROVIDER_BODY_SENTINEL_E9C0";
        var options = CreateValidOptions();
        options.ApiKey = apiKey;
        var baselineRequest = CreateRequest();
        var request = baselineRequest with
        {
            Question = baselineRequest.Question with
            {
                QuestionText = $"{question} {jwt}",
                CorrectAnswer = answer,
                Solution = password,
                ExpectedReasoning = reasoning
            },
            StudentSubmission = baselineRequest.StudentSubmission with
            {
                FinalAnswer = answer,
                ReasoningText = reasoning
            }
        };
        var logs = new CapturingLoggerProvider();
        var rawResponseClient = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromResult(ProviderResult(
                $"{{\"feedback\":\"{feedback}\""))
        };

        await Assert.ThrowsAsync<AIAnalysisValidationException>(
            () => CreateService(rawResponseClient, options, logProvider: logs)
                .AnalyzeReasoningAsync(request, CancellationToken.None));

        var failedClient = new FakeGeminiGenerateContentClient
        {
            Handler = (_, _, _, _) => Task.FromException<GeminiGenerateContentResult>(
                new InvalidOperationException(providerDiagnostic))
        };
        await Assert.ThrowsAsync<GeminiAdapterException>(
            () => CreateService(failedClient, options, logProvider: logs)
                .AnalyzeReasoningAsync(request, CancellationToken.None));

        Assert.Equal(2, logs.Entries.Count(entry => entry.State.ContainsKey("Provider")));
        foreach (var entry in logs.Entries)
        {
            Assert.Null(entry.Exception);
            var flattened = Flatten(entry);
            foreach (var sentinel in new[]
                     {
                         apiKey,
                         jwt,
                         password,
                         question,
                         answer,
                         reasoning,
                         feedback,
                         providerDiagnostic
                     })
            {
                Assert.DoesNotContain(sentinel, flattened, StringComparison.Ordinal);
            }
        }
    }

    private static GeminiAIService CreateService(
        FakeGeminiGenerateContentClient client,
        GeminiOptions? options = null,
        IAIAnalysisResponseParser? parser = null,
        CapturingLoggerProvider? logProvider = null,
        ManualTimeProvider? timeProvider = null,
        ILogger<GeminiAIService>? logger = null)
    {
        logProvider ??= new CapturingLoggerProvider();
        timeProvider ??= new ManualTimeProvider();
        logger ??= (ILogger<GeminiAIService>)logProvider.CreateLogger(
            typeof(GeminiAIService).FullName!);

        return new GeminiAIService(
            Options.Create(options ?? CreateValidOptions()),
            client,
            new GeminiPromptBuilder(),
            new GeminiResponseJsonSchema(),
            parser ?? new StrictAIAnalysisResponseParser(new AnalyzeReasoningResponseValidator()),
            logger,
            timeProvider);
    }

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

    private static GeminiGenerateContentResult ProviderResult(
        string responseText = ValidResponseJson,
        int? promptTokenCount = null,
        int? candidatesTokenCount = null,
        int? totalTokenCount = null) =>
        new(responseText, promptTokenCount, candidatesTokenCount, totalTokenCount);

    private static CapturedLog AssertTerminalEvent(
        CapturingLoggerProvider logs,
        LogLevel level,
        string outcome,
        string? errorCode,
        string? model,
        double latencyMs,
        int? promptTokenCount,
        int? candidatesTokenCount,
        int? totalTokenCount)
    {
        var entry = Assert.Single(logs.Entries, candidate => candidate.State.ContainsKey("Provider"));
        Assert.Equal(level, entry.Level);
        Assert.Equal("Gemini", entry.State["Provider"]);
        Assert.Equal(model, entry.State["Model"]);
        Assert.Equal(latencyMs, entry.State["LatencyMs"]);
        Assert.Equal(promptTokenCount, entry.State["PromptTokenCount"]);
        Assert.Equal(candidatesTokenCount, entry.State["CandidatesTokenCount"]);
        Assert.Equal(totalTokenCount, entry.State["TotalTokenCount"]);
        Assert.Equal(outcome, entry.State["Outcome"]);
        Assert.Equal(errorCode, entry.State["ErrorCode"]);
        Assert.Null(entry.Exception);
        return entry;
    }

    private static string Flatten(CapturedLog entry) => string.Join(
        "|",
        new[] { entry.Message }
            .Concat(entry.State.Select(pair => $"{pair.Key}={pair.Value}"))
            .Concat(entry.Scopes.SelectMany(scope =>
                scope.Select(pair => $"{pair.Key}={pair.Value}")))
            .Concat(entry.Exception is null ? [] : [entry.Exception.ToString()]));

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public int TimestampReadCount { get; private set; }

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp()
        {
            TimestampReadCount++;
            return Interlocked.Read(ref _timestamp);
        }

        public void Advance(TimeSpan elapsed) =>
            Interlocked.Add(ref _timestamp, (long)elapsed.TotalMilliseconds);
    }

    private sealed record CapturedLog(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> State,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Scopes,
        Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly object _gate = new();
        private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

        public List<CapturedLog> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
            _scopeProvider = scopeProvider;

        public void Dispose()
        {
        }

        private void Record<TState>(
            LogLevel logLevel,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<IReadOnlyDictionary<string, object?>>();
            _scopeProvider.ForEachScope(
                (scope, collection) => collection.Add(ToDictionary(scope)),
                scopes);
            var entry = new CapturedLog(
                logLevel,
                formatter(state, exception),
                ToDictionary(state),
                scopes,
                exception);
            lock (_gate)
            {
                Entries.Add(entry);
            }
        }

        private static IReadOnlyDictionary<string, object?> ToDictionary<TState>(TState state) =>
            state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>();

        private sealed class CapturingLogger(CapturingLoggerProvider provider) :
            ILogger,
            ILogger<GeminiAIService>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                provider._scopeProvider.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                provider.Record(logLevel, state, exception, formatter);
        }
    }

    private sealed class FakeGeminiGenerateContentClient : IGeminiGenerateContentClient
    {
        public Func<string, string, GenerateContentConfig, CancellationToken, Task<GeminiGenerateContentResult>> Handler { get; init; } =
            (_, _, _, _) => Task.FromResult(ProviderResult());

        public int CallCount { get; private set; }

        public string? Model { get; private set; }

        public string? Prompt { get; private set; }

        public GenerateContentConfig? Config { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<GeminiGenerateContentResult> GenerateContentAsync(
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
