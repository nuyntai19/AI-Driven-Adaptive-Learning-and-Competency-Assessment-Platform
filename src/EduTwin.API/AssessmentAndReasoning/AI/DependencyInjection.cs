using EduTwin.BLL.AssessmentAndReasoning.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddGeminiAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName));
        services.TryAddSingleton<IAnalyzeReasoningResponseValidator, AnalyzeReasoningResponseValidator>();
        services.TryAddSingleton<IAIAnalysisResponseParser, StrictAIAnalysisResponseParser>();
        services.TryAddSingleton<IGeminiGenerateContentClient, GoogleGenAIGenerateContentClient>();
        services.TryAddSingleton<GeminiPromptBuilder>();
        services.TryAddSingleton<GeminiResponseJsonSchema>();
        services.TryAddSingleton<IAIService, GeminiAIService>();

        return services;
    }
}
