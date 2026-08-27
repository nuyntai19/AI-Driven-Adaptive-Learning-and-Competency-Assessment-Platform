using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed class AIAnalysisOrchestrationDependencyInjectionTests
{
    [Fact]
    public void AddAssessmentAndReasoning_RegistersExactOrchestrationLifetimesWithoutProvider()
    {
        var services = new ServiceCollection();

        services.AddAssessmentAndReasoning();
        services.AddAssessmentAndReasoning();

        AssertDescriptor<IAIAnalysisRequestFactory, AIAnalysisRequestFactory>(
            services,
            ServiceLifetime.Singleton);
        AssertDescriptor<IAIReasoningAnalysisBuilder, AIReasoningAnalysisBuilder>(
            services,
            ServiceLifetime.Singleton);
        AssertDescriptor<IAIAnalysisJobProcessor, AIAnalysisJobProcessor>(
            services,
            ServiceLifetime.Scoped);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IAIService));
    }

    [Fact]
    public void AddAssessmentAndReasoning_WithExplicitFakeAI_ResolvesScopedProcessingGraph()
    {
        var services = new ServiceCollection();
        services.AddIdentityAndTenancy();
        services.AddAssessmentAndReasoning();
        services.AddSingleton<IAIService, FakeAIService>();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<EduTwinDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var processor = scope.ServiceProvider.GetRequiredService<IAIAnalysisJobProcessor>();

        Assert.IsType<AIAnalysisJobProcessor>(processor);
    }

    private static void AssertDescriptor<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    private sealed class FakeAIService : IAIService
    {
        public Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(
            AnalyzeReasoningRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
