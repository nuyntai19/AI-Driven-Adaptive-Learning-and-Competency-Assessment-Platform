using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
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

        var oldName = center.CenterName;
        var oldTimezone = center.Timezone;
        var oldRowVersion = center.RowVersion;

        center.CenterName = centerName;
        center.Timezone = timezone;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        center.UpdatedAt = now;

        var actorUserId = _tenantContext.UserId;
        var auditLog = new AuthorizationAuditLog
        {
            CenterId = center.CenterId,
            TargetCenterId = center.CenterId,
            ActorUserId = actorUserId,
            TargetUserId = null,
            TargetType = "Center",
            TargetId = center.CenterId.ToString("D"),
            ActionType = "CenterMetadataUpdated",
            BeforeData = JsonSerializer.Serialize(new
            {
                CenterName = oldName,
                Timezone = oldTimezone,
                RowVersion = oldRowVersion
            }),
            AfterData = JsonSerializer.Serialize(new
            {
                CenterName = center.CenterName,
                Timezone = center.Timezone,
                RowVersion = center.RowVersion + 1
            }),
            Reason = $"Quản lý trung tâm cập nhật thông tin vận hành (Tên: '{oldName}' -> '{center.CenterName}', Múi giờ: '{oldTimezone}' -> '{center.Timezone}')",
            TraceId = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            CreatedBy = actorUserId
        };
        _dbContext.AuthorizationAuditLogs.Add(auditLog);

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
