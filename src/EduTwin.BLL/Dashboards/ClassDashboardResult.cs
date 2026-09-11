using EduTwin.Contracts.Common;
using EduTwin.Contracts.Dashboards;

namespace EduTwin.BLL.Dashboards;

public sealed class ClassDashboardResult
{
    public bool IsSuccess { get; private init; }
    public ClassDashboardDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }

    public static ClassDashboardResult Success(ClassDashboardDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static ClassDashboardResult ValidationFailed() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed };

    public static ClassDashboardResult NotFound() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };

    public static ClassDashboardResult Forbidden() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource };
}
