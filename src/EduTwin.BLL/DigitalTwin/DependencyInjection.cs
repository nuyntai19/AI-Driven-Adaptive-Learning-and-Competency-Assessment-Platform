using Microsoft.Extensions.DependencyInjection;
using EduTwin.BLL.DigitalTwin.Orchestration;

namespace EduTwin.BLL.DigitalTwin;

public static class DependencyInjection
{
    public static IServiceCollection AddDigitalTwin(this IServiceCollection services)
    {
        services.AddScoped<IGoalIdGenerator, CryptographicGoalIdGenerator>();
        services.AddScoped<IUpsertStudentSubjectGoalUseCase, UpsertStudentSubjectGoalUseCase>();
        services.AddScoped<IListStudentSubjectGoalsUseCase, ListStudentSubjectGoalsUseCase>();
        services.AddSingleton<IBehaviorCalibrationCalculator, BehaviorCalibrationCalculator>();
        services.AddScoped<IBehaviorCalibrationSampleProvider, BehaviorCalibrationSampleProvider>();
        services.AddScoped<IBehaviorTwinUpdater, BehaviorTwinUpdater>();
        services.AddScoped<IKnowledgeTwinUpdater, KnowledgeTwinUpdater>();
        services.AddScoped<IStudentGoalRiskUpdater, StudentGoalRiskUpdater>();
        services.AddScoped<IStudentTwinUpdater, StudentTwinUpdater>();
        services.AddScoped<ITwinUpdateHistoryWriter, TwinUpdateHistoryWriter>();
        services.AddScoped<ITwinCompletionOrchestrator, TwinCompletionOrchestrator>();
        return services;
    }
}
