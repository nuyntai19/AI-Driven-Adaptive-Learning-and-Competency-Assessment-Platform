using EduTwin.BLL.AssessmentAndReasoning.AI;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GeminiAIService : IAIService, IAIAnalysisProfile, IPartitionedAIService, IAIAnalysisProvenance
{
    public string ProviderName => _batcher?.ProviderName ?? "Gemini";
    public string ModelName => _batcher?.ModelName ?? _options.Model ?? "";
    public Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(AnalyzeReasoningRequest request,
        AIAnalysisBatchPartition partition, CancellationToken cancellationToken) =>
        _batcher is null ? AnalyzeReasoningAsync(request, cancellationToken) : _batcher.AnalyzeAsync(request, cancellationToken, partition);
    public string AnalysisProfileVersion => _batcher?.ProfileVersion ?? $"Gemini:{_options.Model}:method-agnostic-v2:temperature-{(_options.Model?.StartsWith("gemini-3", StringComparison.Ordinal) == true ? 1 : 0)}:{AIAnalysisContract.SchemaVersion}";
    private readonly ReasoningMicroBatcher? _batcher;
    private readonly GeminiOptions _options;
    private readonly IGeminiGenerateContentClient _client;
    private readonly GeminiPromptBuilder _promptBuilder;
    private readonly GeminiResponseJsonSchema _responseJsonSchema;
    private readonly IAIAnalysisResponseParser _responseParser;
    private readonly ILogger<GeminiAIService> _logger;
    private readonly TimeProvider _timeProvider;

    public GeminiAIService(
        IOptions<GeminiOptions> options,
        IGeminiGenerateContentClient client,
        GeminiPromptBuilder promptBuilder,
        GeminiResponseJsonSchema responseJsonSchema,
        IAIAnalysisResponseParser responseParser,
        ILogger<GeminiAIService> logger,
        TimeProvider timeProvider,
        ReasoningMicroBatcher? batcher = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(promptBuilder);
        ArgumentNullException.ThrowIfNull(responseJsonSchema);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options.Value;
        _client = client;
        _promptBuilder = promptBuilder;
        _responseJsonSchema = responseJsonSchema;
        _responseParser = responseParser;
        _logger = logger;
        _timeProvider = timeProvider;
        _batcher = batcher;
    }

    public async Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(
        AnalyzeReasoningRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_batcher is not null) return await _batcher.AnalyzeAsync(request, cancellationToken);
        var startedTimestamp = _timeProvider.GetTimestamp();
        string? model = null;
        GeminiGenerateContentResult? providerResult = null;

        try
        {
            _options.Validate();
            model = _options.Model!;

            var prompt = _promptBuilder.Build(request);
            var config = _responseJsonSchema.CreateGenerateContentConfig();
            if (model.StartsWith("gemini-3", StringComparison.Ordinal)) config.Temperature = 1;
            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCancellation.CancelAfter(_options.Timeout);

            try
            {
                providerResult = request.StudentSubmission.ImageParts.Count == 0
                    ? await _client.GenerateContentAsync(
                        model,
                        prompt,
                        config,
                        linkedCancellation.Token)
                    : await _client.GenerateContentWithImagesAsync(
                        model,
                        prompt,
                        request.StudentSubmission.ImageParts
                            .Select(image => new GeminiInlineImagePart(image.Data, image.MimeType))
                            .ToArray(),
                        config,
                        linkedCancellation.Token);
                cancellationToken.ThrowIfCancellationRequested();

                if (linkedCancellation.IsCancellationRequested)
                {
                    throw GeminiAdapterException.Timeout();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw GeminiAdapterException.Timeout();
            }
            catch (AIAnalysisDeferredException)
            {
                throw;
            }
            catch (AIAnalysisInfrastructureException)
            {
                throw;
            }
            catch (GeminiAdapterException)
            {
                throw;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
            catch
            {
                throw GeminiAdapterException.RequestFailed();
            }

            if (string.IsNullOrWhiteSpace(providerResult.ResponseText))
            {
                throw GeminiAdapterException.ResponseEmpty();
            }

            var response = _responseParser.ParseAndValidate(providerResult.ResponseText, request);
            LogTerminalEvent(
                LogLevel.Information,
                startedTimestamp,
                model,
                providerResult,
                "Succeeded",
                null);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogTerminalEvent(
                LogLevel.Information,
                startedTimestamp,
                model,
                providerResult,
                "Canceled",
                "AI_PROVIDER_CALL_CANCELED");
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (AIAnalysisInfrastructureException)
        {
            throw;
        }
        catch (AIAnalysisDeferredException exception)
        {
            LogTerminalEvent(LogLevel.Information, startedTimestamp, model, providerResult, "Deferred", exception.ErrorCode);
            throw;
        }
        catch (GeminiAdapterException exception)
        {
            LogTerminalEvent(
                LogLevel.Warning,
                startedTimestamp,
                model,
                providerResult,
                "Failed",
                exception.ErrorCode);
            throw;
        }
        catch (AIAnalysisValidationException exception)
        {
            LogTerminalEvent(
                LogLevel.Warning,
                startedTimestamp,
                model,
                providerResult,
                "Failed",
                exception.ErrorCode);
            throw;
        }
        catch
        {
            const string errorCode = "AI_PROVIDER_REQUEST_FAILED";
            LogTerminalEvent(
                LogLevel.Warning,
                startedTimestamp,
                model,
                providerResult,
                "Failed",
                errorCode);
            throw GeminiAdapterException.RequestFailed();
        }
    }

    private void LogTerminalEvent(
        LogLevel level,
        long startedTimestamp,
        string? model,
        GeminiGenerateContentResult? result,
        string outcome,
        string? errorCode)
    {
        var latencyMs = Math.Max(
            0d,
            _timeProvider.GetElapsedTime(startedTimestamp).TotalMilliseconds);

        _logger.Log(
            level,
            "Gemini provider invocation completed for {Provider} model {Model} in {LatencyMs} ms with prompt tokens {PromptTokenCount}, candidate tokens {CandidatesTokenCount}, total tokens {TotalTokenCount}, outcome {Outcome}, and error code {ErrorCode}.",
            "Gemini",
            model,
            latencyMs,
            NormalizeTokenCount(result?.PromptTokenCount),
            NormalizeTokenCount(result?.CandidatesTokenCount),
            NormalizeTokenCount(result?.TotalTokenCount),
            outcome,
            errorCode);
    }

    private static int? NormalizeTokenCount(int? count) =>
        count is >= 0 ? count : null;
}
