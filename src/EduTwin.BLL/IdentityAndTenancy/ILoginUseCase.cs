using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface ILoginUseCase
{
    Task<LoginResult> ExecuteAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default);
}
