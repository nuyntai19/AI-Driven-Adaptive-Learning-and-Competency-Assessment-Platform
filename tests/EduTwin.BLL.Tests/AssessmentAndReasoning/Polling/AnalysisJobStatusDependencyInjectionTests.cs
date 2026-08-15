using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.Polling;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Polling;

public sealed class AnalysisJobStatusDependencyInjectionTests
{
    [Fact]
    public void AddAssessmentAndReasoning_RegistersPollingUseCaseOnceAsScoped()
    {
        var services = new ServiceCollection();
        services.AddIdentityAndTenancy();
        services.AddAssessmentAndReasoning();
        services.AddDbContext<EduTwinDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType
                == typeof(IGetAnalysisJobStatusUseCase));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(GetAnalysisJobStatusUseCase), descriptor.ImplementationType);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider
            .GetRequiredService<IGetAnalysisJobStatusUseCase>();
        Assert.IsType<GetAnalysisJobStatusUseCase>(resolved);
    }
}
