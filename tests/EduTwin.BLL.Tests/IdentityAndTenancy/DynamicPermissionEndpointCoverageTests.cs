using System.Reflection;
using EduTwin.API.Controllers;
using EduTwin.API.Security;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class DynamicPermissionEndpointCoverageTests
{
    [Fact]
    public void EveryBusinessEndpoint_HasDynamicPermissionPolicy()
    {
        var permissionCodes = AuthorizationPermissionCatalog
            .CreatePermissions()
            .Select(item => item.PermissionCode)
            .ToHashSet(StringComparer.Ordinal);
        var compositePolicies = CompositePermissionPolicies
            .GetAnyPermissionPolicies()
            .Keys
            .ToHashSet(StringComparer.Ordinal);
        var excludedControllers = new HashSet<Type>
        {
            typeof(AuthController),
            typeof(HealthController)
        };
        var violations = new List<string>();

        foreach (var controllerType in typeof(AuthController).Assembly
                     .GetTypes()
                     .Where(type => type.Namespace == typeof(AuthController).Namespace &&
                                    type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                                    !excludedControllers.Contains(type)))
        {
            foreach (var method in controllerType
                         .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                         .Where(item => item.GetCustomAttributes<HttpMethodAttribute>().Any()))
            {
                var policies = method
                    .GetCustomAttributes<AuthorizeAttribute>()
                    .Select(attribute => attribute.Policy)
                    .Where(policy => !string.IsNullOrWhiteSpace(policy))
                    .Cast<string>()
                    .ToArray();
                if (!policies.Any(policy =>
                        permissionCodes.Contains(policy) || compositePolicies.Contains(policy)))
                {
                    violations.Add($"{controllerType.Name}.{method.Name}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void LegacyRolePolicy_IsOnlyAdditionalStudentSubmitBoundary()
    {
        var uses = typeof(AuthController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(AuthController).Namespace &&
                           type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>()
                    .Where(attribute => AuthorizationPolicies
                        .GetPolicyRoles()
                        .ContainsKey(attribute.Policy ?? string.Empty))
                    .Select(attribute => $"{type.Name}.{method.Name}:{attribute.Policy}")))
            .ToArray();

        Assert.Equal(
            ["LearningController.SubmitAttempt:StudentOnly"],
            uses);
    }
}
