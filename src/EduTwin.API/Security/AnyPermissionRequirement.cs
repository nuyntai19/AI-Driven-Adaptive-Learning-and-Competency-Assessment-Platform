using Microsoft.AspNetCore.Authorization;

namespace EduTwin.API.Security;

public sealed record AnyPermissionRequirement(IReadOnlyList<string> PermissionCodes)
    : IAuthorizationRequirement;
