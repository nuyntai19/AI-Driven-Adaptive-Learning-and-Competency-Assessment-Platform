using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class UpdateCenterProfileUseCase : IUpdateCenterProfileUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public UpdateCenterProfileUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateCenterProfileResult> ExecuteAsync(UpdateCenterProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved)
        {
            return UpdateCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
        {
            return UpdateCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal))
        {
            return UpdateCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerName = request.CenterName?.Trim();
        var timezone = request.Timezone?.Trim();
        var rowVersionText = request.RowVersion;

        if (string.IsNullOrEmpty(centerName) || centerName.Length > 200 ||
            string.IsNullOrEmpty(timezone) || timezone.Length > 64 ||
            string.IsNullOrWhiteSpace(rowVersionText))
        {
            return UpdateCenterProfileResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!ulong.TryParse(rowVersionText, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedRowVersion) || expectedRowVersion == 0)
        {
            return UpdateCenterProfileResult.Failure(ErrorCodes.ValidationFailed);
        }

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .Where(c => c.CenterId == centerId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (center == null)
        {
            return UpdateCenterProfileResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (center.RowVersion != expectedRowVersion)
        {
            return UpdateCenterProfileResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        center.CenterName = centerName;
        center.Timezone = timezone;
        center.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UpdateCenterProfileResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        return UpdateCenterProfileResult.Success(
            center.CenterId.ToString("D").ToLowerInvariant(),
            center.CenterCode,
            center.CenterName,
            center.Status.ToString(),
            center.Timezone,
            center.RowVersion.ToString(CultureInfo.InvariantCulture)
        );
    }
}
