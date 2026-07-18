using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IUpdateCenterProfileUseCase
{
    Task<UpdateCenterProfileResult> ExecuteAsync(UpdateCenterProfileRequest request, CancellationToken cancellationToken = default);
}
