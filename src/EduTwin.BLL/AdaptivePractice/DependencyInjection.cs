using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.AdaptivePractice;

public static class DependencyInjection
{
    public static IServiceCollection AddAdaptivePractice(this IServiceCollection services)
    {
        services.AddScoped<IAdaptivePracticeEngine, AdaptivePracticeEngine>();
        services.AddScoped<EduTwin.BLL.AssessmentAndReasoning.Bookmarks.StudentQuestionBookmarkService>();
        return services;
    }
}
