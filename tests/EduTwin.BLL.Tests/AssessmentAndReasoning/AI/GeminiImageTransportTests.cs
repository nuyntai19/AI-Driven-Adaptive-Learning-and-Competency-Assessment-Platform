using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiImageTransportTests
{
    [Fact]
    public async Task ProductionAdapterAndOfficialSdkSerializeEveryImageByteInOrder()
    {
        // This handler never opens a socket; no credentials/provider quota are used.
        using var handler = new CaptureHandler();
        using var sdk = new Client(apiKey: "offline-test-key", clientOptions: new ClientOptions
        { HttpClientFactory = () => new HttpClient(handler, disposeHandler: false) });
        using var adapter = new GoogleGenAIGenerateContentClient(Options.Create(new GeminiOptions
        { ApiKey = "offline-test-key", Model = "offline-model" }));
        var clients = (ConcurrentDictionary<string, Client>)typeof(GoogleGenAIGenerateContentClient)
            .GetField("_clients", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
        clients["offline-test-key"] = sdk;
        var questionImage = new byte[] { 137, 80, 78, 71, 1 };
        var studentImage = new byte[] { 137, 80, 78, 71, 2, 3 };
        await adapter.GenerateContentWithImagesAsync("offline-model", "test prompt", [new(questionImage, "image/png"), new(studentImage, "image/png")], new GeminiResponseJsonSchema().CreateGenerateContentConfig(), CancellationToken.None);
        using var document = JsonDocument.Parse(handler.Body!);
        var parts = document.RootElement.GetProperty("contents")[0].GetProperty("parts");
        Assert.Equal(3, parts.GetArrayLength());
        Assert.Equal("test prompt", parts[0].GetProperty("text").GetString());
        Assert.Equal(questionImage, Convert.FromBase64String(parts[1].GetProperty("inlineData").GetProperty("data").GetString()!));
        Assert.Equal(studentImage, Convert.FromBase64String(parts[2].GetProperty("inlineData").GetProperty("data").GetString()!));
        Assert.Equal("image/png", parts[2].GetProperty("inlineData").GetProperty("mimeType").GetString());
        var evidenceSchema = document.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema")
            .GetProperty("properties").GetProperty("visualEvidence");
        Assert.False(evidenceSchema.TryGetProperty("maxItems", out _));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task TextOnlyAdaptersCannotSilentlyDiscardImages()
    {
        IGeminiGenerateContentClient adapter = new TextOnlyAdapter();
        await adapter.GenerateContentWithImagesAsync("test", "prompt", [], new GenerateContentConfig(), CancellationToken.None);
        await Assert.ThrowsAsync<NotSupportedException>(async () => await adapter.GenerateContentWithImagesAsync("test", "prompt",
            [new([1], "image/png")], new GenerateContentConfig(), CancellationToken.None));
    }

    private sealed class TextOnlyAdapter : IGeminiGenerateContentClient
    {
        public Task<GeminiGenerateContentResult> GenerateContentAsync(string model, string prompt, GenerateContentConfig config, CancellationToken cancellationToken)
            => Task.FromResult(new GeminiGenerateContentResult("{}", null, null, null));
    }
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"{}\"}]},\"finishReason\":\"STOP\"}],\"usageMetadata\":{\"promptTokenCount\":10,\"candidatesTokenCount\":1,\"totalTokenCount\":11}}", Encoding.UTF8, "application/json") };
        }
    }
}
