using EduTwin.API.AssessmentAndReasoning.AI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class HandwritingTranscriptionServiceTests
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    [Fact]
    public async Task TranscribeSendsPngToVisionClientAndReturnsTrimmedText()
    {
        var client = new FakeClient(new GeminiGenerateContentResult("  $y' = 2x + 1$  ", null, null, null));
        var service = new HandwritingTranscriptionService(client,
            Options.Create(new GeminiOptions { ApiKey = "test-key", Model = "test-model" }));

        var text = await service.TranscribeAsync(PngBytes, CancellationToken.None);

        Assert.Equal("$y' = 2x + 1$", text);
        Assert.Equal("test-model", client.LastModel);
        Assert.Single(client.LastImages!);
        Assert.Equal("image/png", client.LastImages![0].MimeType);
        Assert.Equal(PngBytes, client.LastImages![0].Data);
    }

    [Fact]
    public async Task TranscribeWithoutConfiguredKeyFailsWithConfigurationErrorAndNeverCallsProvider()
    {
        var client = new FakeClient(new GeminiGenerateContentResult("must not be used", null, null, null));
        var service = new HandwritingTranscriptionService(client,
            Options.Create(new GeminiOptions { Model = "test-model" }));

        var ex = await Assert.ThrowsAsync<GeminiAdapterException>(
            () => service.TranscribeAsync(PngBytes, CancellationToken.None));

        Assert.Equal("AI_PROVIDER_CONFIGURATION_INVALID", ex.ErrorCode);
        Assert.Null(client.LastModel);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("not-base64!!!", false)]
    public void TryDecodePngRejectsInvalidInput(string? input, bool expected)
    {
        Assert.Equal(expected, HandwritingTranscriptionService.TryDecodePng(input, out _));
    }

    [Fact]
    public void TryDecodePngAcceptsRawBase64AndDataUrl()
    {
        var base64 = Convert.ToBase64String(PngBytes);
        Assert.True(HandwritingTranscriptionService.TryDecodePng(base64, out var raw));
        Assert.Equal(PngBytes, raw);
        Assert.True(HandwritingTranscriptionService.TryDecodePng($"data:image/png;base64,{base64}", out var dataUrl));
        Assert.Equal(PngBytes, dataUrl);
    }

    [Fact]
    public void TryDecodePngRejectsNonPngBytes()
    {
        var notPng = Convert.ToBase64String("this is not a png"u8.ToArray());
        Assert.False(HandwritingTranscriptionService.TryDecodePng(notPng, out _));
    }

    private sealed class FakeClient(GeminiGenerateContentResult result) : IGeminiGenerateContentClient
    {
        public string? LastModel { get; private set; }
        public IReadOnlyList<GeminiInlineImagePart>? LastImages { get; private set; }

        public Task<GeminiGenerateContentResult> GenerateContentAsync(
            string model, string prompt, GenerateContentConfig config, CancellationToken cancellationToken)
            => Task.FromResult(result);

        public Task<GeminiGenerateContentResult> GenerateContentWithImagesAsync(
            string model, string prompt, IReadOnlyList<GeminiInlineImagePart> images,
            GenerateContentConfig config, CancellationToken cancellationToken)
        {
            LastModel = model;
            LastImages = images;
            return Task.FromResult(result);
        }
    }
}
