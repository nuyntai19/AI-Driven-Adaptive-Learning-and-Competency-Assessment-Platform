using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduTwin.API.AssessmentAndReasoning.Background;

public static class DependencyInjection
{
    public static IServiceCollection AddAIAnalysisJobBackgroundWorker(
        this IServiceCollection services,
        Action<AIAnalysisJobWorkerOptions>? configure = null,
        AIAnalysisJobWorkerIdentity? identity = null)
    {
        var options = new AIAnalysisJobWorkerOptions();
        configure?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(options);
        services.TryAddSingleton(
            identity ?? new AIAnalysisJobWorkerIdentity($"worker-{Guid.NewGuid():N}"));
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, AIAnalysisJobBackgroundService>());

        return services;
    }
}
