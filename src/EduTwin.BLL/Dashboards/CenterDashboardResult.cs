using EduTwin.Contracts.Common;
using EduTwin.Contracts.Dashboards;

namespace EduTwin.BLL.Dashboards;

public sealed class CenterDashboardResult
{
    public bool IsSuccess { get; private init; }
    public CenterDashboardDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static CenterDashboardResult Success(CenterDashboardDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static CenterDashboardResult ValidationFailed(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed, ErrorMessage = message };

    public static CenterDashboardResult NotFound(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound, ErrorMessage = message };

    public static CenterDashboardResult Forbidden(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource, ErrorMessage = message };
}
