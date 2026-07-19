using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IGetClassUseCase
{
    Task<GetClassResult> ExecuteAsync(Guid classId, CancellationToken cancellationToken = default);
}
