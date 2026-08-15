using EduTwin.API.AssessmentAndReasoning.Background;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Background;

public sealed class AIAnalysisJobBackgroundDependencyInjectionTests
{
    [Fact]
    public void AddAssessmentAndReasoning_RegistersWorkerBLLServicesWithRequiredLifetimes()
    {
        var services = new ServiceCollection();

        services.AddAssessmentAndReasoning();

        AssertDescriptor<IAIAnalysisJobStateMachine>(services, ServiceLifetime.Singleton);
        AssertDescriptor<IRuleBasedFallbackBuilder>(services, ServiceLifetime.Singleton);
        AssertDescriptor<IAIAnalysisJobCandidateDiscovery>(services, ServiceLifetime.Scoped);
        AssertDescriptor<IAIAnalysisJobLeaseOperation>(services, ServiceLifetime.Scoped);
        AssertDescriptor<IAIAnalysisJobProcessor>(services, ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddAIAnalysisJobBackgroundWorker_CalledTwice_RegistersExactlyOneHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);

        services.AddAIAnalysisJobBackgroundWorker(
            identity: new AIAnalysisJobWorkerIdentity("worker-di"));
        services.AddAIAnalysisJobBackgroundWorker(
            identity: new AIAnalysisJobWorkerIdentity("worker-ignored"));

        var hostedDescriptors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(AIAnalysisJobBackgroundService))
            .ToArray();
        Assert.Single(hostedDescriptors);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        Assert.IsType<AIAnalysisJobBackgroundService>(
            Assert.Single(provider.GetServices<IHostedService>()));
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    public void WorkerOptions_InvalidValue_FailsFast(
        int pollMilliseconds,
        int leaseMilliseconds,
        int batchSize,
        int perCenterBatchSize)
    {
        var options = new AIAnalysisJobWorkerOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(pollMilliseconds),
            LeaseDuration = TimeSpan.FromMilliseconds(leaseMilliseconds),
            BatchSize = batchSize,
            PerCenterBatchSize = perCenterBatchSize
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void WorkerIdentity_NormalizesAndEnforcesSchemaLength()
    {
        var identity = new AIAnalysisJobWorkerIdentity(" \tworker\r\n-test\0 ");

        Assert.Equal("worker-test", identity.Value);
        Assert.Throws<ArgumentException>(
            () => new AIAnalysisJobWorkerIdentity(new string('w', 101)));
    }

    private static void AssertDescriptor<TService>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(TService));
        Assert.Equal(lifetime, descriptor.Lifetime);
    }
}
