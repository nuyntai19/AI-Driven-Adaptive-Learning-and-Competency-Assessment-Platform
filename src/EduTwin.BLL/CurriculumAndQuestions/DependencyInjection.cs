using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.CurriculumAndQuestions;

public static class DependencyInjection
{
    public static IServiceCollection AddCurriculumAndQuestions(this IServiceCollection services)
    {
        services.AddScoped<ICreateCurriculumUseCase, CreateCurriculumUseCase>();
        services.AddScoped<IListCurriculumsUseCase, ListCurriculumsUseCase>();
        services.AddScoped<IGetCurriculumUseCase, GetCurriculumUseCase>();
        services.AddScoped<IUpdateCurriculumUseCase, UpdateCurriculumUseCase>();
        services.AddScoped<IAssignCurriculumClassesUseCase, AssignCurriculumClassesUseCase>();
        services.AddScoped<IAssignCurriculumNodesUseCase, AssignCurriculumNodesUseCase>();
        services.AddScoped<IPublishCurriculumUseCase, PublishCurriculumUseCase>();
        
        services.AddScoped<ICreateQuestionUseCase, CreateQuestionUseCase>();
        services.AddScoped<IGetQuestionUseCase, GetQuestionUseCase>();
        services.AddScoped<IListQuestionsUseCase, ListQuestionsUseCase>();
        services.AddScoped<IUpdateQuestionUseCase, UpdateQuestionUseCase>();
        services.AddScoped<IActivateQuestionUseCase, ActivateQuestionUseCase>();
        services.AddScoped<IArchiveQuestionUseCase, ArchiveQuestionUseCase>();
        services.AddScoped<IDeleteQuestionUseCase, DeleteQuestionUseCase>();
        
        return services;
    }
}
