using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface ICreateClassUseCase
{
    Task<CreateClassResult> ExecuteAsync(CreateClassRequest request, CancellationToken cancellationToken = default);
}
