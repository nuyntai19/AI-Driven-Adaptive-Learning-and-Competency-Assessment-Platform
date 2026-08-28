using Google.GenAI.Types;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed record GeminiGenerateContentResult(
    string ResponseText,
    int? PromptTokenCount,
    int? CandidatesTokenCount,
    int? TotalTokenCount);

public interface IGeminiGenerateContentClient
{
    Task<GeminiGenerateContentResult> GenerateContentAsync(
        string model,
        string prompt,
        GenerateContentConfig config,
        CancellationToken cancellationToken);
}
