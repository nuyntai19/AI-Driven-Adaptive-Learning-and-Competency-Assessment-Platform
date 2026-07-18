using System.Collections.Generic;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public static class AuthorizationPolicies
{
    public const string StudentOnly = "StudentOnly";
    public const string TeacherOnly = "TeacherOnly";
    public const string CenterManagerOnly = "CenterManagerOnly";
    public const string TeacherOrCenterManager = "TeacherOrCenterManager";

    public static IReadOnlyDictionary<string, UserRole[]> GetPolicyRoles()
    {
        return new Dictionary<string, UserRole[]>
        {
            { StudentOnly, new[] { UserRole.Student } },
            { TeacherOnly, new[] { UserRole.Teacher } },
            { CenterManagerOnly, new[] { UserRole.CenterManager } },
            { TeacherOrCenterManager, new[] { UserRole.Teacher, UserRole.CenterManager } }
        };
    }
}
