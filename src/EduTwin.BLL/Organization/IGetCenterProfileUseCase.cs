using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IGetCenterProfileUseCase
{
    Task<GetCenterProfileResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
