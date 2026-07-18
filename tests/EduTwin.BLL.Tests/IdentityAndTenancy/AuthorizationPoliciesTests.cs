using System;
using System.Linq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class AuthorizationPoliciesTests
{
    [Fact]
    public void StudentOnly_Should_Contain_Only_StudentRole()
    {
        var policies = AuthorizationPolicies.GetPolicyRoles();
        Assert.True(policies.ContainsKey(AuthorizationPolicies.StudentOnly));

        var roles = policies[AuthorizationPolicies.StudentOnly];
        Assert.Single(roles);
        Assert.Contains(UserRole.Student, roles);
    }

    [Fact]
    public void TeacherOnly_Should_Contain_Only_TeacherRole()
    {
        var policies = AuthorizationPolicies.GetPolicyRoles();
        Assert.True(policies.ContainsKey(AuthorizationPolicies.TeacherOnly));

        var roles = policies[AuthorizationPolicies.TeacherOnly];
        Assert.Single(roles);
        Assert.Contains(UserRole.Teacher, roles);
    }

    [Fact]
    public void CenterManagerOnly_Should_Contain_Only_CenterManagerRole()
    {
        var policies = AuthorizationPolicies.GetPolicyRoles();
        Assert.True(policies.ContainsKey(AuthorizationPolicies.CenterManagerOnly));

        var roles = policies[AuthorizationPolicies.CenterManagerOnly];
        Assert.Single(roles);
        Assert.Contains(UserRole.CenterManager, roles);
    }

    [Fact]
    public void TeacherOrCenterManager_Should_Contain_Teacher_And_CenterManager()
    {
        var policies = AuthorizationPolicies.GetPolicyRoles();
        Assert.True(policies.ContainsKey(AuthorizationPolicies.TeacherOrCenterManager));

        var roles = policies[AuthorizationPolicies.TeacherOrCenterManager];
        Assert.Equal(2, roles.Length);
        Assert.Contains(UserRole.Teacher, roles);
        Assert.Contains(UserRole.CenterManager, roles);
    }

    [Fact]
    public void Policies_Should_NotContain_Invalid_Roles()
    {
        // By using the strongly-typed UserRole enum array, it is impossible at compile time
        // to put an invalid string role here, guaranteeing adherence to the frozen enum.
        var policies = AuthorizationPolicies.GetPolicyRoles();
        foreach(var kvp in policies)
        {
            foreach(var role in kvp.Value)
            {
                Assert.True(Enum.IsDefined(typeof(UserRole), role), $"Policy {kvp.Key} contains undefined role {role}");
            }
        }
    }

    [Fact]
    public void Policies_Should_Have_Unique_Names()
    {
        var names = new[]
        {
            AuthorizationPolicies.StudentOnly,
            AuthorizationPolicies.TeacherOnly,
            AuthorizationPolicies.CenterManagerOnly,
            AuthorizationPolicies.TeacherOrCenterManager
        };
        Assert.Equal(names.Length, names.Distinct().Count());
    }
}
