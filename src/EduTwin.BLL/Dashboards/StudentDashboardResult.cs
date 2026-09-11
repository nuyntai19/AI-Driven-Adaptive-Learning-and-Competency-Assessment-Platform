using EduTwin.Contracts.Common;
using EduTwin.Contracts.Dashboards;

namespace EduTwin.BLL.Dashboards;

public sealed class StudentDashboardResult
{
    public bool IsSuccess { get; private init; }
    public StudentDashboardDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }

    public static StudentDashboardResult Success(StudentDashboardDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static StudentDashboardResult ValidationFailed() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed };

    public static StudentDashboardResult NotFound() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };

    public static StudentDashboardResult Forbidden() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource };
}
