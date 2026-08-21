using Google.GenAI.Types;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public interface IGeminiGenerateContentClient
{
    Task<string> GenerateContentAsync(
        string model,
        string prompt,
        GenerateContentConfig config,
        CancellationToken cancellationToken);
}
