using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IGetCurrentUserUseCase
{
    Task<GetCurrentUserResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
