using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Platform;

namespace EduTwin.BLL.Platform;

public interface IPlatformCenterService
{
    Task<PlatformResult<PlatformCentersListData>> ListCentersAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<PlatformCenterListItemDto>> CreateCenterAsync(
        CreatePlatformCenterRequest request,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<PlatformCenterListItemDto>> UpdateCenterStatusAsync(
        Guid centerId,
        UpdatePlatformCenterStatusRequest request,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<ResetCenterManagerPasswordData>> ResetCenterManagerPasswordAsync(
        Guid centerId,
        Guid managerUserId,
        ResetCenterManagerPasswordRequest request,
        string traceId,
        CancellationToken cancellationToken = default);
}
