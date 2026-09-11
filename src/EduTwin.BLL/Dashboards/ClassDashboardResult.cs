using EduTwin.Contracts.Common;
using EduTwin.Contracts.Dashboards;

namespace EduTwin.BLL.Dashboards;

public sealed class ClassDashboardResult
{
    public bool IsSuccess { get; private init; }
    public ClassDashboardDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ClassDashboardResult Success(ClassDashboardDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static ClassDashboardResult ValidationFailed(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed, ErrorMessage = message };

    public static ClassDashboardResult NotFound(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound, ErrorMessage = message };

    public static ClassDashboardResult Forbidden(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource, ErrorMessage = message };
}
