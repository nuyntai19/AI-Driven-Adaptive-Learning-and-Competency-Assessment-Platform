using Microsoft.AspNetCore.Authorization;

namespace EduTwin.API.Security;

public sealed record PermissionRequirement(string PermissionCode)
    : IAuthorizationRequirement;
