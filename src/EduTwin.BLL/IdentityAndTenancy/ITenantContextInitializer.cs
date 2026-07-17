using System;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface ITenantContextInitializer
{
    void Initialize(Guid centerId, Guid userId, string role, int authVersion);
}
