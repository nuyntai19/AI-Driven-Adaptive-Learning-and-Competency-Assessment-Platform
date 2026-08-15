using EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Polling;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.AssessmentAndReasoning;

public static class DependencyInjection
{
    public static IServiceCollection AddAssessmentAndReasoning(this IServiceCollection services)
    {
        services.AddSingleton<MultipleChoiceGrader>();
        services.AddSingleton<ShortAnswerGrader>();
        services.AddSingleton<EssayGrader>();
        services.AddSingleton<PreliminaryGraderFactory>();
        services.AddSingleton<IAIAnalysisJobStateMachine, AIAnalysisJobStateMachine>();
        services.AddSingleton<IRuleBasedFallbackBuilder, RuleBasedFallbackBuilder>();
        services.AddScoped<IAIAnalysisJobCandidateDiscovery, AIAnalysisJobCandidateDiscovery>();
        services.AddScoped<IAIAnalysisJobLeaseOperation, AIAnalysisJobLeaseOperation>();
        services.AddScoped<IAIAnalysisJobProcessor, AIAnalysisJobProcessor>();
        services.AddScoped<IListAttemptsUseCase, ListAttemptsUseCase>();
        services.AddScoped<IGetAnalysisJobStatusUseCase, GetAnalysisJobStatusUseCase>();
        services.AddScoped<IAttemptSubmissionValidator, AttemptSubmissionValidator>();
        services.AddScoped<ISubmitAttemptUseCase, SubmitAttemptUseCase>();

        return services;
    }
}
