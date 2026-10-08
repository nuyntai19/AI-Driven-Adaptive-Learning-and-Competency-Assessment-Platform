using System.Collections.Concurrent;
using System.Text;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GoogleGenAIGenerateContentClient : IGeminiGenerateContentClient, IDisposable
{
    private readonly GeminiOptions _options;
    private readonly ILogger<GoogleGenAIGenerateContentClient>? _logger;
    private readonly GeminiQuotaCoordinator? _quota;
    private readonly GeminiCredentialAvailability _availability;
    private readonly ConcurrentDictionary<string, Client> _clients = new();
    private readonly SemaphoreSlim _localSlots;
    private int _requestCounter;
    private bool _disposed;

    public GoogleGenAIGenerateContentClient(IOptions<GeminiOptions> options,
        ILogger<GoogleGenAIGenerateContentClient>? logger = null, GeminiQuotaCoordinator? quota = null,
        TimeProvider? timeProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _quota = quota;
        _availability = new(timeProvider ?? TimeProvider.System);
        // Production uses the shared SQL gate. Standalone clients still respect
        // the same limit locally instead of silently having no capacity guard.
        _localSlots = new(Math.Clamp(_options.MaxConcurrentRequests, 1, 32));
    }

    public Task<GeminiGenerateContentResult> GenerateContentAsync(string model, string prompt,
        GenerateContentConfig config, CancellationToken token) => GenerateAsync(model, prompt, [], config, token);

    public Task<GeminiGenerateContentResult> GenerateContentWithImagesAsync(string model, string prompt,
        IReadOnlyList<GeminiInlineImagePart> images, GenerateContentConfig config, CancellationToken token) =>
        GenerateAsync(model, prompt, images, config, token);

    private async Task<GeminiGenerateContentResult> GenerateAsync(string model, string prompt,
        IReadOnlyList<GeminiInlineImagePart> images, GenerateContentConfig config, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _options.Validate();
        // Every caller, including learning-path enrichment, must stop before its
        // SQL capacity lease expires. The deadline spans all credential rotation.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(_options.Timeout);
        try { return await GenerateCoreAsync(model, prompt, images, config, deadline.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw GeminiAdapterException.Timeout(); }
    }

    private async Task<GeminiGenerateContentResult> GenerateCoreAsync(string model, string prompt,
        IReadOnlyList<GeminiInlineImagePart> images, GenerateContentConfig config, CancellationToken token)
    {
        if (model.StartsWith("gemini-3", StringComparison.Ordinal)) config.Temperature = 1;
        var keys = _options.GetAllApiKeys();
        var pools = _options.GetQuotaPools(keys.Count);
        var start = (uint)Interlocked.Increment(ref _requestCounter);
        var parts = new List<Part> { new() { Text = prompt } };
        foreach (var image in images)
        {
            if (image.Data.Length == 0 || image.MimeType != "image/png") throw GeminiAdapterException.RequestFailed();
            parts.Add(new Part { InlineData = new Blob { Data = image.Data, MimeType = image.MimeType } });
        }

        // Admission estimate, reconciled from provider usage; it is not an exact tokenizer.
        var estimatedTokens = Encoding.UTF8.GetByteCount(prompt) / 2L + 1 + images.Count * 8192L;
        var unavailablePools = new HashSet<string>(StringComparer.Ordinal);
        var waits = new List<(TimeSpan Delay, bool Daily)>();
        for (var n = 0; n < keys.Count; n++)
        {
            token.ThrowIfCancellationRequested();
            var index = (int)((start + (uint)n) % (uint)keys.Count);
            if (!_availability.IsAvailable(keys[index], model)) continue;
            var pool = pools.Single(p => p.KeyIndexes.Contains(index));
            if (unavailablePools.Contains(pool.ProjectId)) continue;
            GeminiQuotaLease? lease = null;
            var localSlot = false;
            var transient = false;
            var quotaFailure = false;
            var dailyQuota = false;
            TimeSpan? providerRetryAfter = null;
            int? actualTokens = null;
            TimeSpan? failedPoolDelay = null;
            try
            {
                if (_quota is not null)
                {
                    try { lease = await _quota.AcquireAsync(pool, model, estimatedTokens, _options.Timeout, token, _options.MaxConcurrentRequests); }
                    catch (Exception exception) when (exception is not OperationCanceledException and not AIAnalysisDeferredException and not GeminiAdapterException)
                    { throw new AIAnalysisInfrastructureException(); }
                }
                else
                {
                    localSlot = await _localSlots.WaitAsync(0, token);
                    if (!localSlot) throw new AIAnalysisDeferredException(TimeSpan.FromSeconds(2), blocksAllPools: true);
                }
                var client = _clients.GetOrAdd(keys[index], key => new Client(apiKey: key,
                    httpOptions: new HttpOptions { RetryOptions = new HttpRetryOptions { Attempts = 1 } }));
                var response = images.Count == 0
                    ? await client.Models.GenerateContentAsync(model, prompt, config, token)
                    : await client.Models.GenerateContentAsync(model, new Content { Parts = parts }, config, token);
                token.ThrowIfCancellationRequested();
                actualTokens = response.UsageMetadata?.PromptTokenCount;
                RecordTokens("input", actualTokens);
                RecordTokens("output", response.UsageMetadata?.CandidatesTokenCount);
                RecordTokens("thinking", response.UsageMetadata?.ThoughtsTokenCount);
                RecordTokens("cached_input", response.UsageMetadata?.CachedContentTokenCount);
                return new(response.Text ?? "", actualTokens, response.UsageMetadata?.CandidatesTokenCount,
                    response.UsageMetadata?.TotalTokenCount);
            }
            catch (AIAnalysisDeferredException deferred)
            {
                if (deferred.BlocksAllPools) throw; // A full global gate cannot be helped by rotating keys.
                unavailablePools.Add(pool.ProjectId);
                waits.Add((deferred.RetryAfter, deferred.ErrorCode == "AI_PROVIDER_DAILY_QUOTA_WAIT"));
            }
            catch (OperationCanceledException) { throw; }
            catch (AIAnalysisInfrastructureException) { throw; }
            catch (GeminiAdapterException) { throw; }
            catch (Exception ex)
            {
                var status = StatusCode(ex);
                transient = status is 408 or 429 or >= 500 || (ex is HttpRequestException && status == 0);
                quotaFailure = status == 429;
                if (quotaFailure)
                {
                    var failure = GeminiQuotaFailureClassifier.Classify(ex.Message);
                    dailyQuota = failure.Daily;
                    providerRetryAfter = failure.RetryAfter;
                }
                _logger?.LogWarning("Gemini request failed at credential index {KeyIndex}; status {Status}, type {ExceptionType}.",
                    index, status, ex.GetType().Name);
                // In particular, newer projects may not have access to legacy 2.5 models (404).
                // Try another eligible project, never repeat the rejected key or switch model silently.
                if (_availability.MarkFailure(keys[index], model, status))
                {
                    AIProcessingMetrics.Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", "credential_model_unavailable"));
                    continue;
                }
                if (!transient) throw GeminiAdapterException.RequestFailed();
                unavailablePools.Add(pool.ProjectId);
                failedPoolDelay = TimeSpan.FromSeconds(quotaFailure ? 60 : 2);
                if (providerRetryAfter > failedPoolDelay) failedPoolDelay = providerRetryAfter;
                AIProcessingMetrics.Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", dailyQuota ? "provider_daily_quota" : quotaFailure ? "provider_429" : "provider_transient"));
            }
            finally
            {
                if (lease is not null && _quota is not null)
                {
                    using var releaseTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        var cooldown = await _quota.CompleteAsync(lease, actualTokens, transient, quotaFailure,
                            releaseTimeout.Token, providerRetryAfter, dailyQuota);
                        if (failedPoolDelay.HasValue && cooldown.HasValue) failedPoolDelay = cooldown;
                    }
                    catch (Exception ex) { _logger?.LogWarning("Provider reservation release failed with {ExceptionType}; reservation will expire.", ex.GetType().Name); }
                }
                if (localSlot) _localSlots.Release();
                if (failedPoolDelay.HasValue) waits.Add((failedPoolDelay.Value, dailyQuota));
            }
        }
        if (waits.Count > 0)
            throw new AIAnalysisDeferredException(waits.Min(w => w.Delay),
                waits.All(w => w.Daily) ? "AI_PROVIDER_DAILY_QUOTA_WAIT" : "AI_PROVIDER_CAPACITY_WAIT");
        throw GeminiAdapterException.RequestFailed();
    }

    private static int StatusCode(Exception ex) => ex switch
    {
        ClientError error => error.StatusCode,
        ServerError error => error.StatusCode,
        HttpRequestException error => (int?)error.StatusCode ?? 0,
        _ => 0
    };

    private static void RecordTokens(string kind, int? count)
    {
        if (count.HasValue) AIProcessingMetrics.Tokens.Add(count.Value, new KeyValuePair<string, object?>("kind", kind));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var client in _clients.Values) client.Dispose();
        _clients.Clear();
        _localSlots.Dispose();
    }
}
