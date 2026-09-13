using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

public static class DependencyInjection
{
    public static IServiceCollection AddAttemptAttachmentStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AttachmentStorageOptions>().Bind(configuration.GetSection(AttachmentStorageOptions.SectionName));
        var options = configuration.GetSection(AttachmentStorageOptions.SectionName)
            .Get<AttachmentStorageOptions>() ?? new AttachmentStorageOptions();
        var dataProtection = services.AddDataProtection().SetApplicationName("EduTwin");
        if (!string.IsNullOrWhiteSpace(options.DataProtectionKeysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.DataProtectionKeysPath));
        }
        services.TryAddSingleton<IAttemptAttachmentTokenService, DataProtectionAttemptAttachmentTokenService>();
        services.TryAddSingleton<IAttemptAttachmentStorage, FileSystemAttemptAttachmentStorage>();
        return services;
    }
}
