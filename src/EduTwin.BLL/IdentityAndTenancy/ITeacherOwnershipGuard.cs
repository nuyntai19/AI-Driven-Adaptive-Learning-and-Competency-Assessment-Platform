using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface ITeacherOwnershipGuard
{
    Task<OwnershipDecision> CheckTeacherAccessAsync(Guid teacherId, CancellationToken cancellationToken);
}
