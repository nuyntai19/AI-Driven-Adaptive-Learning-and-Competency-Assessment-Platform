using System;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface ITenantContext
{
    Guid? CenterId { get; }
    Guid? UserId { get; }
    string? Role { get; }
    int? AuthVersion { get; }
    bool IsResolved { get; }
}
