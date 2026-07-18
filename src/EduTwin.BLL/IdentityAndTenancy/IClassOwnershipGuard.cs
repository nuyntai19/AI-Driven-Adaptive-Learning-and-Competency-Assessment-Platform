using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IClassOwnershipGuard
{
    Task<OwnershipDecision> CheckClassAccessAsync(Guid classId, CancellationToken cancellationToken);
}
