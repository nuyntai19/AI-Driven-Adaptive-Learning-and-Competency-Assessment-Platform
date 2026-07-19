using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IGetTeacherUseCase
{
    Task<GetTeacherResult> ExecuteAsync(Guid teacherId, CancellationToken cancellationToken = default);
}
