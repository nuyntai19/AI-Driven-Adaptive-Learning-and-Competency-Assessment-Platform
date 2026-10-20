using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.CurriculumAndQuestions;

public static class DependencyInjection
{
    public static IServiceCollection AddCurriculumAndQuestions(this IServiceCollection services)
    {
        services.AddScoped<CurriculumApplicationUseCase>();
        services.AddScoped<ICreateCurriculumUseCase, CreateCurriculumUseCase>();
        services.AddScoped<IListCurriculumsUseCase, ListCurriculumsUseCase>();
        services.AddScoped<IGetCurriculumUseCase, GetCurriculumUseCase>();
        services.AddScoped<IUpdateCurriculumUseCase, UpdateCurriculumUseCase>();
        services.AddScoped<IAssignCurriculumClassesUseCase, AssignCurriculumClassesUseCase>();
        services.AddScoped<IAssignCurriculumNodesUseCase, AssignCurriculumNodesUseCase>();
        services.AddScoped<IPublishCurriculumUseCase, PublishCurriculumUseCase>();
        services.AddScoped<IArchiveCurriculumUseCase, ArchiveCurriculumUseCase>();
        services.AddScoped<ICloneCurriculumUseCase, CloneCurriculumUseCase>();
        
        services.AddScoped<ICreateQuestionUseCase, CreateQuestionUseCase>();
        services.AddScoped<IGetQuestionUseCase, GetQuestionUseCase>();
        services.AddScoped<GetQuestionImageUseCase>();
        services.AddScoped<IListQuestionsUseCase, ListQuestionsUseCase>();
        services.AddScoped<IUpdateQuestionUseCase, UpdateQuestionUseCase>();
        services.AddScoped<IActivateQuestionUseCase, ActivateQuestionUseCase>();
        services.AddScoped<IArchiveQuestionUseCase, ArchiveQuestionUseCase>();
        services.AddScoped<IDeleteQuestionUseCase, DeleteQuestionUseCase>();
        services.AddScoped<IQuestionActivationPolicy, QuestionActivationPolicy>();
        services.AddSingleton<Import.QuestionImportMediaStore>(_ => new Import.QuestionImportMediaStore());
        services.AddSingleton<Import.QuestionImportImageNormalizer>();
        services.AddScoped<Import.QuestionImportUseCase>();
        services.AddScoped<Import.IQuestionImportUseCase>(sp => sp.GetRequiredService<Import.QuestionImportUseCase>());
        services.AddScoped<Import.IQuestionImportV2UseCase>(sp => sp.GetRequiredService<Import.QuestionImportUseCase>());
        
        return services;
    }
}
