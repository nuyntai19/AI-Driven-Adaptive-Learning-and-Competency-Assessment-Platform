using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IDeleteTeacherUseCase
{
    Task<DeleteTeacherResult> ExecuteAsync(Guid teacherId, CancellationToken cancellationToken = default);
}
