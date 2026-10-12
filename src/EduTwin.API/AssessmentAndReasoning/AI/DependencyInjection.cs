using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.Recommendations;
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
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .Configure(options =>
            {
                options.ListKey = configuration[GeminiOptions.ListKeyConfigurationName];
                options.LoadQuotaPoolsJson(configuration["GEMINI_QUOTA_POOLS"]);
            });
        services.TryAddSingleton<IAnalyzeReasoningResponseValidator, AnalyzeReasoningResponseValidator>();
        services.AddOptions<AIGradingOptions>().Bind(configuration.GetSection("AIGrading"));
        services.AddHttpClient("GroqGrading", client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.TryAddSingleton<GroqGenerateContentClient>();
        services.TryAddSingleton<IReasoningBatchExecutor, ReasoningBatchExecutor>();
        services.TryAddSingleton<ReasoningMicroBatcher>();
        services.TryAddSingleton<IAIAnalysisResponseParser, StrictAIAnalysisResponseParser>();
        services.TryAddSingleton<IGeminiGenerateContentClient, GoogleGenAIGenerateContentClient>();
        services.TryAddSingleton<GeminiQuotaCoordinator>();
        services.TryAddSingleton<GeminiPromptBuilder>();
        services.TryAddSingleton<GeminiResponseJsonSchema>();
        services.TryAddSingleton<IAIService, GeminiAIService>();
        services.TryAddScoped<ILearningPathPlanEnricher, GeminiLearningPathPlanEnricher>();

        return services;
    }
}
