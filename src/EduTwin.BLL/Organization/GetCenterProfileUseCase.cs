using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.Organization;

public class GetCenterProfileUseCase : IGetCenterProfileUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetCenterProfileUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<GetCenterProfileResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved)
        {
            return GetCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
        {
            return GetCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (string.IsNullOrEmpty(_tenantContext.Role))
        {
            return GetCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal))
        {
            return GetCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .Where(c => c.CenterId == centerId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (center == null)
        {
            return GetCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        return GetCenterProfileResult.Success(
            center.CenterId.ToString("D"),
            center.CenterCode,
            center.CenterName,
            center.Status.ToString(),
            center.Timezone,
            center.RowVersion.ToString(CultureInfo.InvariantCulture)
        );
    }
}
