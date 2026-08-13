using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
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
        services.AddScoped<IAttemptSubmissionValidator, AttemptSubmissionValidator>();
        services.AddScoped<ISubmitAttemptUseCase, SubmitAttemptUseCase>();

        return services;
    }
}
