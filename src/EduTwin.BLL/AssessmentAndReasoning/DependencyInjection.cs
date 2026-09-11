using EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.AssessmentAndReasoning.Polling;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduTwin.BLL.AssessmentAndReasoning;

public static class DependencyInjection
{
    public static IServiceCollection AddAssessmentAndReasoning(this IServiceCollection services)
    {
        services.TryAddSingleton<MultipleChoiceGrader>();
        services.TryAddSingleton<ShortAnswerGrader>();
        services.TryAddSingleton<EssayGrader>();
        services.TryAddSingleton<PreliminaryGraderFactory>();
        services.TryAddSingleton<IAIAnalysisJobStateMachine, AIAnalysisJobStateMachine>();
        services.TryAddSingleton<IEvidenceGate, EvidenceGate>();
        services.TryAddSingleton<IEvidenceConsistencyChecker, EvidenceConsistencyChecker>();
        services.TryAddSingleton<IEvidenceAssessmentFactory, EvidenceAssessmentFactory>();
        services.TryAddSingleton<IRuleBasedFallbackBuilder, RuleBasedFallbackBuilder>();
        services.TryAddSingleton<IAIAnalysisRequestFactory, AIAnalysisRequestFactory>();
        services.TryAddSingleton<IAIReasoningAnalysisBuilder, AIReasoningAnalysisBuilder>();
        services.TryAddScoped<IAIAnalysisJobCandidateDiscovery, AIAnalysisJobCandidateDiscovery>();
        services.TryAddScoped<IAIAnalysisJobLeaseOperation, AIAnalysisJobLeaseOperation>();
        services.TryAddScoped<IAIAnalysisJobProcessor, AIAnalysisJobProcessor>();
        services.TryAddScoped<IListAttemptsUseCase, ListAttemptsUseCase>();
        services.TryAddScoped<IListTeacherReviewQueueUseCase, ListTeacherReviewQueueUseCase>();
        services.TryAddScoped<ITeacherOverrideUseCase, TeacherOverrideUseCase>();
        services.TryAddScoped<IGetAnalysisJobStatusUseCase, GetAnalysisJobStatusUseCase>();
        services.TryAddScoped<IAttemptSubmissionValidator, AttemptSubmissionValidator>();
        services.TryAddScoped<ISubmitAttemptUseCase, SubmitAttemptUseCase>();
        services.TryAddScoped<Feedback.IGetAttemptFeedbackUseCase, Feedback.GetAttemptFeedbackUseCase>();

        return services;
    }
}
