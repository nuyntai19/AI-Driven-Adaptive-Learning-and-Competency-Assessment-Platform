using System;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IBackgroundTenantScopeFactory
{
    IDisposable BeginScope(Guid centerId);
}
