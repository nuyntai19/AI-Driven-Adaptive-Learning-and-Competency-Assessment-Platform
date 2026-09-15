using System;
using System.Globalization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

internal static class CurriculumGuards
{
    public static bool TryResolveActor(
        ITenantContext tenantContext,
        out Guid centerId,
        out Guid actorId,
        out bool isTeacher)
    {
        centerId = tenantContext.CenterId ?? Guid.Empty;
        actorId = tenantContext.UserId ?? Guid.Empty;
        isTeacher = string.Equals(tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        return tenantContext.IsResolved &&
               centerId != Guid.Empty &&
               actorId != Guid.Empty &&
               (isTeacher || string.Equals(tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal));
    }

    public static bool CanAccess(Curriculum curriculum, Guid actorId, bool isTeacher) =>
        !isTeacher || curriculum.TeacherId == actorId;

    public static bool TryParseRowVersion(string? raw, out ulong rowVersion)
    {
        rowVersion = 0;
        if (string.IsNullOrEmpty(raw))
            return false;

        foreach (var ch in raw)
        {
            if (ch < '0' || ch > '9')
                return false;
        }

        return ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out rowVersion) && rowVersion > 0;
    }
}
