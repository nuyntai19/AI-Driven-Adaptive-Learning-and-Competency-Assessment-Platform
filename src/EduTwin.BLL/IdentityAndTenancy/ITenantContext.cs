using System;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface ITenantContext
{
    Guid? CenterId { get; }
    Guid? UserId { get; }
    string? Role { get; }
    uint? AuthVersion { get; }
    bool IsResolved { get; }
}
