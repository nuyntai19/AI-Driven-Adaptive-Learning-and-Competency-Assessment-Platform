using Microsoft.Extensions.DependencyInjection;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.BLL.Recommendations.UseCases;

namespace EduTwin.BLL.Recommendations;

public static class DependencyInjection
{
    public static IServiceCollection AddRecommendations(this IServiceCollection services)
    {
        services.AddScoped<IOpportunityCandidateBuilder, OpportunityCandidateBuilder>();
        services.AddSingleton<ILinearFallbackSelector, LinearFallbackSelector>();
        services.AddScoped<IAdaptiveQuestionSelector, AdaptiveQuestionSelector>();
        services.AddScoped<RecommendationEngine>();
        services.AddScoped<IRecommendationEngine>(provider => new V4RecommendationEngine(provider.GetRequiredService<RecommendationEngine>()));

        services.AddScoped<ILearningSessionExecutionService, LearningSessionExecutionService>();
        services.AddScoped<Planning.ILearningPathV4Planner, Planning.LearningPathV4Planner>();
        services.AddScoped<ILearningPathV4Evaluator, LearningPathV4Evaluator>();
        services.AddScoped<ILearningActivityExecutionService, LearningActivityExecutionService>();

        services.AddScoped<IGetNextQuestionUseCase, GetNextQuestionUseCase>();
        services.AddScoped<IGetActiveRecommendationUseCase, GetActiveRecommendationUseCase>();
        services.AddScoped<IAcceptRecommendationUseCase, AcceptRecommendationUseCase>();
        services.AddScoped<IDismissRecommendationUseCase, DismissRecommendationUseCase>();
        services.AddScoped<IGetActiveLearningPathUseCase, GetActiveLearningPathUseCase>();
        services.AddScoped<IGetLearningPathTopicsUseCase, GetLearningPathTopicsUseCase>();
        services.AddScoped<IGetStudentLearningPathPreferenceUseCase, GetStudentLearningPathPreferenceUseCase>();
        services.AddScoped<IGetDetailedLearningPathUseCase, GenerateLearningPathUseCase>();
        services.AddScoped<IUpdateLearningPathSessionUseCase, UpdateLearningPathSessionUseCase>();

        return services;
    }
}
