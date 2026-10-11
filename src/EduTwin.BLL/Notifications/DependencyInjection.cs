using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddScoped<NotificationInboxService>();
        services.AddScoped<ActivityService>();
        services.AddScoped<NotificationOperationsService>();
        services.AddScoped<NotificationOutbox>();
        services.AddScoped<NotificationDispatcher>();
        services.AddScoped<NotificationAudiencePolicy>();
        services.AddScoped<NotificationManualService>();
        return services;
    }
}
