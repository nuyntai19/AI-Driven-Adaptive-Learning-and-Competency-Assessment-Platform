using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

// Explicitly selected, approved profile only; no silent downgrade or image-dropping fallback.
public sealed class GroqGenerateContentClient(IHttpClientFactory clients, IOptions<AIGradingOptions> options,
    GeminiQuotaCoordinator quota)
{
    public async Task<GeminiGenerateContentResult> GenerateAsync(string model, string prompt,
        IReadOnlyList<GeminiInlineImagePart> images, CancellationToken token)
    {
        var settings = options.Value;
        var pool = new GeminiQuotaPoolOptions
        {
            ProjectId = "Groq:" + settings.GroqOrganizationId, KeyIndexes = [0], MaxConcurrentRequests = 2,
            RequestsPerMinute = settings.GroqRequestsPerMinute, RequestsPerDay = settings.GroqRequestsPerDay,
            InputTokensPerMinute = settings.GroqTokensPerMinute, TokensPerDay = settings.GroqTokensPerDay,
            RollingDailyWindow = true
        };
        const int outputBudget = 2048;
        var estimate = Encoding.UTF8.GetByteCount(prompt) / 2L + 1 + images.Count * 2048L + outputBudget;
        GeminiQuotaLease lease;
        try { lease = await quota.AcquireAsync(pool, model, estimate, TimeSpan.FromSeconds(30), token); }
        catch (Exception ex) when (ex is not (OperationCanceledException or AIAnalysisDeferredException or GeminiAdapterException))
        { throw new AIAnalysisInfrastructureException(); }
        var transient = false;
        var rateLimited = false;
        TimeSpan? retryAfter = null;
        int? totalTokens = null;
        try
        {
            var content = new List<object> { new { type = "text", text = prompt } };
            foreach (var image in images)
            {
                if (image.MimeType != "image/png" || image.Data.Length == 0) throw GeminiAdapterException.RequestFailed();
                content.Add(new { type = "image_url", image_url = new { url = "data:image/png;base64," + Convert.ToBase64String(image.Data) } });
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.GroqApiKey);
            request.Content = JsonContent.Create(new
            {
                model, messages = new[] { new { role = "user", content } }, temperature = 0,
                max_completion_tokens = outputBudget, response_format = new { type = "json_object" }
            });
            using var response = await clients.CreateClient("GroqGrading").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests;
                transient = rateLimited || (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout;
                if (!transient) throw GeminiAdapterException.RequestFailed();
                retryAfter = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
                throw new AIAnalysisDeferredException(retryAfter.HasValue && retryAfter > TimeSpan.Zero ? retryAfter.Value : TimeSpan.FromSeconds(rateLimited ? 60 : 2));
            }
            if (response.Content.Headers.ContentLength > 1024 * 1024) throw GeminiAdapterException.ResponseInvalid();
            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            var root = doc.RootElement;
            var choice = root.GetProperty("choices")[0];
            if (choice.GetProperty("finish_reason").GetString() != "stop") throw GeminiAdapterException.ResponseInvalid();
            var text = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
            var usage = root.GetProperty("usage");
            var input = usage.GetProperty("prompt_tokens").GetInt32();
            var output = usage.GetProperty("completion_tokens").GetInt32();
            totalTokens = usage.GetProperty("total_tokens").GetInt32();
            AIProcessingMetrics.Tokens.Add(input, new KeyValuePair<string, object?>("provider", "Groq"), new KeyValuePair<string, object?>("kind", "input"));
            AIProcessingMetrics.Tokens.Add(output, new KeyValuePair<string, object?>("provider", "Groq"), new KeyValuePair<string, object?>("kind", "output"));
            return new(text, input, output, totalTokens);
        }
        catch (HttpRequestException) { transient = true; throw new AIAnalysisDeferredException(TimeSpan.FromSeconds(2)); }
        catch (JsonException) { throw GeminiAdapterException.ResponseInvalid(); }
        finally
        {
            using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            // Release failures recover via lease expiry; never discard a valid provider result.
            try { await quota.CompleteAsync(lease, totalTokens, transient, rateLimited, release.Token, retryAfter); }
            catch (Exception) { /* SQL lease expiry is the recovery path. */ }
        }
    }
}
