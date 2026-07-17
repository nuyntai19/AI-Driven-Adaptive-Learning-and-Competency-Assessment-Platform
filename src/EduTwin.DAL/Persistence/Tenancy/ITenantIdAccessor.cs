using System;

namespace EduTwin.DAL.Persistence.Tenancy;

public interface ITenantIdAccessor
{
    Guid? CenterId { get; }
}
