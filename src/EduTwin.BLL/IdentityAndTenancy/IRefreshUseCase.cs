using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IRefreshUseCase
{
    Task<LoginResult> ExecuteAsync(string? rawToken, string? clientIp, CancellationToken cancellationToken = default);
}
