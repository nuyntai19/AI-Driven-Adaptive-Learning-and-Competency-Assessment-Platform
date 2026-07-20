using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IUpdateClassUseCase
{
    Task<UpdateClassResult> ExecuteAsync(Guid classId, UpdateClassRequest request, CancellationToken cancellationToken = default);
}
