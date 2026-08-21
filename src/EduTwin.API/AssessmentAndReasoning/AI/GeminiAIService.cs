using System.Text.Json;
using System.Text.Json.Serialization;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GeminiAIService : IAIService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly GeminiOptions _options;
    private readonly IGeminiGenerateContentClient _client;
    private readonly GeminiPromptBuilder _promptBuilder;
    private readonly GeminiResponseJsonSchema _responseJsonSchema;

    public GeminiAIService(
        IOptions<GeminiOptions> options,
        IGeminiGenerateContentClient client,
        GeminiPromptBuilder promptBuilder,
        GeminiResponseJsonSchema responseJsonSchema)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(promptBuilder);
        ArgumentNullException.ThrowIfNull(responseJsonSchema);

        _options = options.Value;
        _client = client;
        _promptBuilder = promptBuilder;
        _responseJsonSchema = responseJsonSchema;
    }

    public async Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(
        AnalyzeReasoningRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _options.Validate();

        var prompt = _promptBuilder.Build(request);
        var config = _responseJsonSchema.CreateGenerateContentConfig();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCancellation.CancelAfter(_options.Timeout);

        string responseText;
        try
        {
            responseText = await _client.GenerateContentAsync(
                _options.Model!,
                prompt,
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
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (OperationCanceledException)
        {
            throw GeminiAdapterException.Timeout();
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

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw GeminiAdapterException.ResponseEmpty();
        }

        try
        {
            return JsonSerializer.Deserialize<AnalyzeReasoningResponse>(responseText, SerializerOptions)
                ?? throw GeminiAdapterException.ResponseInvalid();
        }
        catch (JsonException)
        {
            throw GeminiAdapterException.ResponseInvalid();
        }
        catch (NotSupportedException)
        {
            throw GeminiAdapterException.ResponseInvalid();
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        return options;
    }
}
