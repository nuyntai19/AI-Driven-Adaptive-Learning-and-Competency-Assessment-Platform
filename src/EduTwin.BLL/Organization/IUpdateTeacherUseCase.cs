using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IUpdateTeacherUseCase
{
    Task<UpdateTeacherResult> ExecuteAsync(Guid teacherId, UpdateTeacherRequest request, CancellationToken cancellationToken = default);
}
