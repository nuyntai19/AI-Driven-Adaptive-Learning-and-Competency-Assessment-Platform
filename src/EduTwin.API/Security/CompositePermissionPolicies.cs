using System.Collections.Generic;

namespace EduTwin.API.Security;

public static class CompositePermissionPolicies
{
    public const string AttemptsRead = "permissions:any:learning.attempts.read";
    public const string StudentTwinRead = "permissions:any:twin.student.read";
    public const string StudentTwinUpdate = "permissions:any:twin.student.update";
    public const string DashboardsClassRead = "permissions:any:dashboards.class.read";

    public static IReadOnlyDictionary<string, string[]> GetAnyPermissionPolicies() =>
        new Dictionary<string, string[]>
        {
            [AttemptsRead] =
            [
                "learning.attempts.read_own",
                "learning.attempts.read_scoped"
            ],
            [StudentTwinRead] =
            [
                "twin.student.read_own",
                "twin.student.read_scoped"
            ],
            [StudentTwinUpdate] =
            [
                "twin.student.update_own",
                "twin.student.update_scoped"
            ],
            [DashboardsClassRead] =
            [
                "dashboards.teacher.read_scoped",
                "dashboards.center.read"
            ]
        };
}
