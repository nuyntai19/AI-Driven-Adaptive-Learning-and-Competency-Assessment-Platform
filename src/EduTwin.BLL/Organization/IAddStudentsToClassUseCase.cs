using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IAddStudentsToClassUseCase
{
    Task<AddStudentsToClassResult> ExecuteAsync(Guid classId, AddStudentsToClassRequest request, CancellationToken cancellationToken = default);
}
