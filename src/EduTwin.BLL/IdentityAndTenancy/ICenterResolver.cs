using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface ICenterResolver
{
    Task<CenterResolutionDto?> ResolveByCodeAsync(string centerCode, CancellationToken cancellationToken = default);
}
