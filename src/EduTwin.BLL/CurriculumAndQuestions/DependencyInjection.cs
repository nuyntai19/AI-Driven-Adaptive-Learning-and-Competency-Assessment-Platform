using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.CurriculumAndQuestions;

public static class DependencyInjection
{
    public static IServiceCollection AddCurriculumAndQuestions(this IServiceCollection services)
    {
        services.AddScoped<ICreateCurriculumUseCase, CreateCurriculumUseCase>();
        return services;
    }
}
