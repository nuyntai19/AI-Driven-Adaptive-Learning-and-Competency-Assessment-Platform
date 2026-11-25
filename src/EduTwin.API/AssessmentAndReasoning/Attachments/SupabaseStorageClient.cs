using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

/// <summary>
/// Singleton-safe Supabase Storage REST client backed by IHttpClientFactory (named client)
/// to avoid captive transient HttpClient instances and ensure DNS/connection rotation.
/// </summary>
public sealed class SupabaseStorageClient : ICloudObjectStorageClient
{
    public const string HttpClientName = "EduTwin.SupabaseStorage";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AttachmentStorageOptions _options;
    private readonly ILogger<SupabaseStorageClient> _logger;

    public SupabaseStorageClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AttachmentStorageOptions> options,
        ILogger<SupabaseStorageClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.CloudBaseUrl) &&
        !string.IsNullOrWhiteSpace(_options.CloudApiKey);

    private HttpClient CreateAuthorizedClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var baseUrl = _options.CloudBaseUrl!.TrimEnd('/');
        client.BaseAddress = new Uri(baseUrl + "/");
        client.DefaultRequestHeaders.Remove("apikey");
        client.DefaultRequestHeaders.Add("apikey", _options.CloudApiKey);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.CloudApiKey);
        return client;
    }

    public async Task UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Supabase Storage is not configured. Skipping upload for {Path}.", path);
            return;
        }

        using var httpClient = CreateAuthorizedClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"object/{bucket}/{path}");
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Supabase Storage upload failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Cloud upload failed: {response.StatusCode} - {errorBody}");
        }
    }

    public async Task MoveAsync(string bucket, string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Supabase Storage is not configured. Skipping move from {Source} to {Dest}.", sourcePath, destinationPath);
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            bucketId = bucket,
            sourceKey = sourcePath,
            destinationKey = destinationPath
        });

        using var httpClient = CreateAuthorizedClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "object/move")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Supabase Storage move failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Cloud move failed: {response.StatusCode} - {errorBody}");
        }
    }

    public async Task<Stream?> DownloadAsync(string bucket, string path, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;

        var requestUri = $"object/authenticated/{bucket}/{path}";
        using var httpClient = CreateAuthorizedClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Supabase Storage download failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
            return null;
        }

        var memoryStream = new MemoryStream();
        await using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            await responseStream.CopyToAsync(memoryStream, cancellationToken);
        }
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task DeleteAsync(string bucket, string path, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return;

        using var httpClient = CreateAuthorizedClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"object/{bucket}/{path}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Supabase Storage delete failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
        }
    }

    public async Task<IReadOnlyList<CloudStorageObjectInfo>> ListObjectsAsync(
        string bucket,
        string prefix,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return Array.Empty<CloudStorageObjectInfo>();
        }

        var payload = JsonSerializer.Serialize(new
        {
            prefix = prefix.Trim('/'),
            limit = Math.Clamp(limit, 1, 100),
            offset = Math.Max(0, offset),
            sortBy = new { column = "name", order = "asc" }
        });

        using var httpClient = CreateAuthorizedClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"object/list/{bucket}")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Supabase Storage list failed for prefix {Prefix} with status {StatusCode}: {Error}", prefix, response.StatusCode, errorBody);
            return Array.Empty<CloudStorageObjectInfo>();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CloudStorageObjectInfo>();
        }

        var results = new List<CloudStorageObjectInfo>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            DateTime? updatedAt = TryParseTimestamp(item, "updated_at");
            DateTime? createdAt = TryParseTimestamp(item, "created_at");
            results.Add(new CloudStorageObjectInfo(name, updatedAt, createdAt));
        }

        return results;
    }

    private static DateTime? TryParseTimestamp(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(el.GetString(), out var dto))
        {
            return dto.UtcDateTime;
        }

        return null;
    }
}
