using EduTwin.Contracts.Common;
using EduTwin.Contracts.Dashboards;

namespace EduTwin.BLL.Dashboards;

public sealed class CenterDashboardResult
{
    public bool IsSuccess { get; private init; }
    public CenterDashboardDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }

    public static CenterDashboardResult Success(CenterDashboardDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static CenterDashboardResult ValidationFailed() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed };

    public static CenterDashboardResult NotFound() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };

    public static CenterDashboardResult Forbidden() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource };
}
