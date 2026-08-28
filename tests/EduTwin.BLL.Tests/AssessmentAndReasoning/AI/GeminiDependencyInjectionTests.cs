using System.Text.RegularExpressions;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using Google.GenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiDependencyInjectionTests
{
    [Fact]
    public void AddGeminiAI_RegistersExactlyOneSemanticValidator()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);

        services.AddGeminiAI(CreateConfiguration());

        AssertSingletonRegistration<IAnalyzeReasoningResponseValidator, AnalyzeReasoningResponseValidator>(services);
    }

    [Fact]
    public void AddGeminiAI_RegistersExactlyOneStrictParser()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);

        services.AddGeminiAI(CreateConfiguration());

        AssertSingletonRegistration<IAIAnalysisResponseParser, StrictAIAnalysisResponseParser>(services);
    }

    [Fact]
    public void AddGeminiAI_CalledTwice_DoesNotDuplicateParserValidatorOrIAIService()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);
        var configuration = CreateConfiguration();

        services.AddGeminiAI(configuration);
        services.AddGeminiAI(configuration);

        AssertSingletonRegistration<IAnalyzeReasoningResponseValidator, AnalyzeReasoningResponseValidator>(services);
        AssertSingletonRegistration<IAIAnalysisResponseParser, StrictAIAnalysisResponseParser>(services);
        AssertSingletonRegistration<IAIService, GeminiAIService>(services);
    }

    [Fact]
    public void AddGeminiAI_RegistersExactlyOneIAIServiceAsGeminiAIService()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);
        var configuration = CreateConfiguration();

        services.AddGeminiAI(configuration);
        services.AddGeminiAI(configuration);

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(IAIService));
        Assert.Equal(typeof(GeminiAIService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddGeminiAI_RegistersOfficialSdkWrapperBehindNarrowInterface()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);

        services.AddGeminiAI(CreateConfiguration());

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(IGeminiGenerateContentClient));
        Assert.Equal(typeof(GoogleGenAIGenerateContentClient), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.DoesNotContain(services, candidate => candidate.ServiceType == typeof(Client));
    }

    [Fact]
    public void AddGeminiAI_KeepsPromptSchemaAndSdkSeamRegistrationsAsSingletons()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);

        services.AddGeminiAI(CreateConfiguration());

        AssertSingletonRegistration<IGeminiGenerateContentClient, GoogleGenAIGenerateContentClient>(services);
        AssertSingletonRegistration<GeminiPromptBuilder, GeminiPromptBuilder>(services);
        AssertSingletonRegistration<GeminiResponseJsonSchema, GeminiResponseJsonSchema>(services);
    }

    [Fact]
    public void AddGeminiAI_BindsModelApiKeyAndTimeoutFromGeminiSection()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);
        services.AddGeminiAI(CreateConfiguration(
            apiKey: "bound-api-key",
            model: "bound-model",
            timeout: "00:00:42"));
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;

        Assert.Equal("bound-api-key", options.ApiKey);
        Assert.Equal("bound-model", options.Model);
        Assert.Equal(TimeSpan.FromSeconds(42), options.Timeout);
    }

    [Fact]
    public void AddGeminiAI_MissingApiKey_DoesNotFailServiceCollectionBuildOrCreateReadinessDependency()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        services.AddGeminiAI(configuration);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var service = provider.GetRequiredService<IAIService>();
        var secondResolution = provider.GetRequiredService<IAIService>();

        Assert.IsType<GeminiAIService>(service);
        Assert.Same(service, secondResolution);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType.FullName?.Contains("HealthCheck", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void AddGeminiAI_ValidConfiguration_ResolvesGeminiAIServiceWithoutCallingProvider()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);
        services.AddGeminiAI(CreateConfiguration());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var service = provider.GetRequiredService<IAIService>();

        Assert.IsType<GeminiAIService>(service);
        Assert.IsType<StrictAIAnalysisResponseParser>(provider.GetRequiredService<IAIAnalysisResponseParser>());
        Assert.IsType<AnalyzeReasoningResponseValidator>(
            provider.GetRequiredService<IAnalyzeReasoningResponseValidator>());
    }

    [Fact]
    public void AddGeminiAI_DoesNotUseBuildServiceProviderOrServiceLocator()
    {
        var services = new ServiceCollection();
        AddRuntimeDependencies(services);

        services.AddGeminiAI(CreateConfiguration());

        var implementationTypes = services
            .Where(descriptor => descriptor.ImplementationType is not null)
            .Select(descriptor => descriptor.ImplementationType!)
            .Where(type => type.Namespace == typeof(GeminiAIService).Namespace)
            .ToArray();
        Assert.All(
            services.Where(descriptor => descriptor.ServiceType.Namespace == typeof(GeminiAIService).Namespace),
            descriptor => Assert.Null(descriptor.ImplementationFactory));
        Assert.DoesNotContain(
            implementationTypes.SelectMany(type => type.GetConstructors()).SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(IServiceProvider));
    }

    [Fact]
    public void Program_RegistersGeminiAdapterExactlyOnce()
    {
        var programPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "EduTwin.API",
            "Program.cs"));
        var programSource = File.ReadAllText(programPath);

        Assert.Single(
            Regex.Matches(
                programSource,
                Regex.Escape("builder.Services.AddGeminiAI(builder.Configuration);"),
                RegexOptions.CultureInvariant).Cast<Match>());
        Assert.DoesNotContain("GeminiHealth", programSource, StringComparison.Ordinal);
    }

    private static IConfiguration CreateConfiguration(
        string apiKey = "test-api-key",
        string model = "test-model",
        string timeout = "00:00:30") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = apiKey,
                ["Gemini:Model"] = model,
                ["Gemini:Timeout"] = timeout
            })
            .Build();

    private static void AddRuntimeDependencies(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
    }

    private static void AssertSingletonRegistration<TService, TImplementation>(IServiceCollection services)
    {
        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}
