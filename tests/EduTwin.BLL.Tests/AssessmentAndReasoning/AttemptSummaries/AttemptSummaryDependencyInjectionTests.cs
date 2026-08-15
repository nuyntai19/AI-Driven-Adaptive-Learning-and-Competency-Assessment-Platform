using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AttemptSummaries;

public sealed class AttemptSummaryDependencyInjectionTests
{
    [Fact]
    public void AddAssessmentAndReasoning_RegistersListUseCaseOnceAsScoped()
    {
        var services = new ServiceCollection();
        services.AddIdentityAndTenancy();
        services.AddAssessmentAndReasoning();
        services.AddDbContext<EduTwinDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(IListAttemptsUseCase));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(ListAttemptsUseCase), descriptor.ImplementationType);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        Assert.IsType<ListAttemptsUseCase>(
            scope.ServiceProvider.GetRequiredService<IListAttemptsUseCase>());
    }
}
