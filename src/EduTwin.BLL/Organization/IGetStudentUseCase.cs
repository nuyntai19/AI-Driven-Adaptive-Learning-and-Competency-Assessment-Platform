using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IGetStudentUseCase
{
    Task<GetStudentResult> ExecuteAsync(Guid studentId, CancellationToken cancellationToken = default);
}
