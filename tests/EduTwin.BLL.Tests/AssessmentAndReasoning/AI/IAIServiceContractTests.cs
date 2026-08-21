using System.Reflection;
using System.Runtime.CompilerServices;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class IAIServiceContractTests
{
    [Fact]
    public void IAIService_IsInterfaceWithExactlyOnePublicMethod()
    {
        Assert.True(typeof(IAIService).IsInterface);
        Assert.Single(typeof(IAIService).GetMethods(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void AnalyzeReasoningAsync_HasExactNameReturnAndParameterTypes()
    {
        var method = Assert.Single(typeof(IAIService).GetMethods(BindingFlags.Instance | BindingFlags.Public));
        var parameters = method.GetParameters();

        Assert.Equal("AnalyzeReasoningAsync", method.Name);
        Assert.Equal(typeof(Task<AnalyzeReasoningResponse>), method.ReturnType);
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(AnalyzeReasoningRequest), parameters[0].ParameterType);
        Assert.Equal("request", parameters[0].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.Equal("cancellationToken", parameters[1].Name);
    }

    [Fact]
    public void AnalyzeReasoningAsync_RequiresCancellationTokenWithoutOptionalDefault()
    {
        var method = Assert.Single(typeof(IAIService).GetMethods(BindingFlags.Instance | BindingFlags.Public));
        var cancellationToken = Assert.Single(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));

        Assert.False(cancellationToken.IsOptional);
        Assert.False(cancellationToken.HasDefaultValue);
    }

    [Fact]
    public void IAIService_PublicSurface_HasNoGeminiHttpDalOrServiceLocatorTypes()
    {
        var publicSurfaceTypes = GetPublicSurfaceTypes(typeof(IAIService));
        var forbiddenTypeNameParts = new[]
        {
            "Gemini",
            "HttpClient",
            "EduTwin.DAL",
            "DbContext",
            "IServiceProvider",
            "ILogger"
        };

        foreach (var type in publicSurfaceTypes)
        {
            var typeName = type.FullName ?? type.Name;
            Assert.DoesNotContain(
                forbiddenTypeNameParts,
                forbidden => typeName.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void RequestResponseTypes_AreSealedAndImmutableByPublicSurface()
    {
        var contractTypes = GetContractTypes(typeof(AnalyzeReasoningRequest), typeof(AnalyzeReasoningResponse));

        Assert.NotEmpty(contractTypes);
        foreach (var type in contractTypes)
        {
            Assert.True(type.IsSealed, $"{type.FullName} must be sealed.");
            Assert.Empty(type.GetFields(BindingFlags.Instance | BindingFlags.Public));
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.NotNull(property.GetMethod);
                Assert.True(property.GetMethod!.IsPublic);
                Assert.NotNull(property.SetMethod);
                Assert.True(property.SetMethod!.IsPublic);
                Assert.Contains(
                    typeof(IsExternalInit),
                    property.SetMethod.ReturnParameter.GetRequiredCustomModifiers());
            }
        }
    }

    [Fact]
    public void Contracts_DoNotRequireDependencyInjectionRegistrationOrConcreteImplementation()
    {
        var concreteImplementations = typeof(IAIService).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IAIService).IsAssignableFrom(type))
            .ToArray();
        var contractTypes = GetContractTypes(typeof(AnalyzeReasoningRequest), typeof(AnalyzeReasoningResponse));

        Assert.Empty(concreteImplementations);
        Assert.All(contractTypes, type =>
            Assert.Contains(
                type.GetConstructors(BindingFlags.Instance | BindingFlags.Public),
                constructor => constructor.GetParameters().Length == 0));
    }

    private static IReadOnlySet<Type> GetPublicSurfaceTypes(Type root)
    {
        var visited = new HashSet<Type>();
        var pending = new Stack<Type>();
        pending.Push(root);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (current.IsGenericType)
            {
                foreach (var genericArgument in current.GetGenericArguments())
                {
                    pending.Push(genericArgument);
                }
            }

            foreach (var method in current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                pending.Push(method.ReturnType);
                foreach (var parameter in method.GetParameters())
                {
                    pending.Push(parameter.ParameterType);
                }
            }

            foreach (var property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                pending.Push(property.PropertyType);
            }
        }

        return visited;
    }

    private static IReadOnlySet<Type> GetContractTypes(params Type[] roots)
    {
        var visited = new HashSet<Type>();
        var pending = new Stack<Type>(roots);

        while (pending.TryPop(out var current))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                pending.Push(current.GetGenericArguments()[0]);
                continue;
            }

            if (current.Namespace != typeof(AnalyzeReasoningRequest).Namespace || !visited.Add(current))
            {
                continue;
            }

            foreach (var property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                pending.Push(property.PropertyType);
            }
        }

        return visited;
    }
}
