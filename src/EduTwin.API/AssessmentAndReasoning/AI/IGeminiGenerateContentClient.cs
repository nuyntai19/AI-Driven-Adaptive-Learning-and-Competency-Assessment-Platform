using Google.GenAI.Types;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed record GeminiGenerateContentResult(
    string ResponseText,
    int? PromptTokenCount,
    int? CandidatesTokenCount,
    int? TotalTokenCount);

public sealed record GeminiInlineImagePart(byte[] Data, string MimeType);

public interface IGeminiGenerateContentClient
{
    Task<GeminiGenerateContentResult> GenerateContentAsync(
        string model,
        string prompt,
        GenerateContentConfig config,
        CancellationToken cancellationToken);

    Task<GeminiGenerateContentResult> GenerateContentWithImagesAsync(
        string model,
        string prompt,
        IReadOnlyList<GeminiInlineImagePart> images,
        GenerateContentConfig config,
        CancellationToken cancellationToken) =>
        images.Count == 0 ? GenerateContentAsync(model, prompt, config, cancellationToken)
            : throw new NotSupportedException("This AI adapter does not support image input; images must never be silently dropped.");
}
