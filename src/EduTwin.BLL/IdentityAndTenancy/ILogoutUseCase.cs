using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface ILogoutUseCase
{
    Task ExecuteAsync(string? rawToken, string? clientIp, CancellationToken cancellationToken = default);
}
