using Microsoft.Extensions.DependencyInjection;
using EduTwin.BLL.Recommendations.UseCases;

namespace EduTwin.BLL.Recommendations;

public static class DependencyInjection
{
    public static IServiceCollection AddRecommendations(this IServiceCollection services)
    {
        services.AddScoped<IOpportunityCandidateBuilder, OpportunityCandidateBuilder>();
        services.AddSingleton<ILinearFallbackSelector, LinearFallbackSelector>();
        services.AddScoped<IAdaptiveQuestionSelector, AdaptiveQuestionSelector>();
        services.AddScoped<IRecommendationEngine, RecommendationEngine>();

        services.AddScoped<IGetNextQuestionUseCase, GetNextQuestionUseCase>();
        services.AddScoped<IGetActiveRecommendationUseCase, GetActiveRecommendationUseCase>();
        services.AddScoped<IAcceptRecommendationUseCase, AcceptRecommendationUseCase>();
        services.AddScoped<IDismissRecommendationUseCase, DismissRecommendationUseCase>();
        services.AddScoped<IGetActiveLearningPathUseCase, GetActiveLearningPathUseCase>();

        return services;
    }
}
