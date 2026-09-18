using System.Collections.Concurrent;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GoogleGenAIGenerateContentClient : IGeminiGenerateContentClient, IDisposable
{
    private readonly GeminiOptions _options;
    private readonly ILogger<GoogleGenAIGenerateContentClient>? _logger;
    private readonly ConcurrentDictionary<string, Client> _clients = new();
    private int _requestCounter;
    private bool _disposed;

    public GoogleGenAIGenerateContentClient(
        IOptions<GeminiOptions> options,
        ILogger<GoogleGenAIGenerateContentClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GeminiGenerateContentResult> GenerateContentAsync(
        string model,
        string prompt,
        GenerateContentConfig config,
        CancellationToken cancellationToken)
    {
        var keys = _options.GetAllApiKeys();
        if (keys.Count == 0)
        {
            throw GeminiAdapterException.ConfigurationInvalid();
        }

        var startIndex = Interlocked.Increment(ref _requestCounter);
        Exception? lastException = null;

        for (var i = 0; i < keys.Count; i++)
        {
            var keyIndex = Math.Abs((startIndex + i) % keys.Count);
            var apiKey = keys[keyIndex];

            try
            {
                var client = GetOrCreateClient(apiKey);
                var response = await client.Models.GenerateContentAsync(
                    model,
                    prompt,
                    config,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                return new GeminiGenerateContentResult(
                    response.Text ?? string.Empty,
                    response.UsageMetadata?.PromptTokenCount,
                    response.UsageMetadata?.CandidatesTokenCount,
                    response.UsageMetadata?.TotalTokenCount);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (GeminiAdapterException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(
                    ex,
                    "Gemini API request failed using key index {KeyIndex} ({KeyPreview}...). Trying next key ({Attempt}/{TotalKeys}). Error: {ErrorMessage}",
                    keyIndex,
                    apiKey[..Math.Min(10, apiKey.Length)],
                    i + 1,
                    keys.Count,
                    ex.Message);
            }
        }

        _logger?.LogError(
            lastException,
            "All {TotalKeys} Gemini API keys failed during GenerateContentAsync.",
            keys.Count);

        throw GeminiAdapterException.RequestFailed();
    }

    public async Task<GeminiGenerateContentResult> GenerateContentWithImagesAsync(
        string model,
        string prompt,
        IReadOnlyList<GeminiInlineImagePart> images,
        GenerateContentConfig config,
        CancellationToken cancellationToken)
    {
        if (images is null || images.Count == 0)
        {
            return await GenerateContentAsync(model, prompt, config, cancellationToken);
        }

        var keys = _options.GetAllApiKeys();
        if (keys.Count == 0)
        {
            throw GeminiAdapterException.ConfigurationInvalid();
        }

        var parts = new List<Part> { new() { Text = prompt } };
        foreach (var image in images)
        {
            if (image.Data is null || image.Data.Length == 0 ||
                !string.Equals(image.MimeType, "image/png", StringComparison.Ordinal))
            {
                throw GeminiAdapterException.RequestFailed();
            }
            parts.Add(new Part { InlineData = new Blob { MimeType = image.MimeType, Data = image.Data } });
        }

        var content = new Content { Parts = parts };
        var startIndex = Interlocked.Increment(ref _requestCounter);
        Exception? lastException = null;

        for (var i = 0; i < keys.Count; i++)
        {
            var keyIndex = Math.Abs((startIndex + i) % keys.Count);
            var apiKey = keys[keyIndex];

            try
            {
                var client = GetOrCreateClient(apiKey);
                var response = await client.Models.GenerateContentAsync(
                    model,
                    content,
                    config,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                return new GeminiGenerateContentResult(
                    response.Text ?? string.Empty,
                    response.UsageMetadata?.PromptTokenCount,
                    response.UsageMetadata?.CandidatesTokenCount,
                    response.UsageMetadata?.TotalTokenCount);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (GeminiAdapterException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(
                    ex,
                    "Gemini multimodal API request failed using key index {KeyIndex} ({KeyPreview}...). Trying next key ({Attempt}/{TotalKeys}). Error: {ErrorMessage}",
                    keyIndex,
                    apiKey[..Math.Min(10, apiKey.Length)],
                    i + 1,
                    keys.Count,
                    ex.Message);
            }
        }

        _logger?.LogError(
            lastException,
            "All {TotalKeys} Gemini API keys failed during GenerateContentWithImagesAsync.",
            keys.Count);

        throw GeminiAdapterException.RequestFailed();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var client in _clients.Values)
        {
            try
            {
                client.Dispose();
            }
            catch
            {
                // ignore
            }
        }
        _clients.Clear();
    }

    private Client GetOrCreateClient(string apiKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw GeminiAdapterException.ConfigurationInvalid();
        }

        return _clients.GetOrAdd(apiKey, key => new Client(apiKey: key));
    }
}
