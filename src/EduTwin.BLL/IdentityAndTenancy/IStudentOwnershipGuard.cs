using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IStudentOwnershipGuard
{
    Task<OwnershipDecision> CheckStudentAccessAsync(Guid studentId, CancellationToken cancellationToken);
}
