using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GoogleGenAIGenerateContentClient : IGeminiGenerateContentClient, IDisposable
{
    private readonly GeminiOptions _options;
    private readonly object _clientLock = new();
    private Client? _client;
    private bool _disposed;

    public GoogleGenAIGenerateContentClient(IOptions<GeminiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public async Task<string> GenerateContentAsync(
        string model,
        string prompt,
        GenerateContentConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await GetOrCreateClient().Models.GenerateContentAsync(
                model,
                prompt,
                config,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw GeminiAdapterException.ResponseEmpty();
            }

            return response.Text;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GeminiAdapterException)
        {
            throw;
        }
        catch
        {
            throw GeminiAdapterException.RequestFailed();
        }
    }

    public void Dispose()
    {
        lock (_clientLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client?.Dispose();
            _client = null;
        }
    }

    private Client GetOrCreateClient()
    {
        lock (_clientLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_client is not null)
            {
                return _client;
            }

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw GeminiAdapterException.ConfigurationInvalid();
            }

            _client = new Client(apiKey: _options.ApiKey);
            return _client;
        }
    }
}
