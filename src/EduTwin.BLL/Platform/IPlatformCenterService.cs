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

    Task<PlatformResult<PlatformCenterManagersListData>> ListCenterManagersAsync(
        Guid centerId,
        int page,
        int pageSize,
        string? status,
        string? search,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<CreateCenterManagerResponseData>> CreateCenterManagerAsync(
        Guid centerId,
        CreateCenterManagerRequest request,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<UpdateCenterManagerStatusData>> UpdateCenterManagerStatusAsync(
        Guid centerId,
        Guid managerUserId,
        UpdateCenterManagerStatusRequest request,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<MakePrimaryCenterManagerData>> MakePrimaryCenterManagerAsync(
        Guid centerId,
        Guid managerUserId,
        MakePrimaryCenterManagerRequest request,
        string traceId,
        CancellationToken cancellationToken = default);
}
