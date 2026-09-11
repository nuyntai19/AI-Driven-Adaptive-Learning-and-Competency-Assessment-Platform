using EduTwin.Contracts.Common;
using EduTwin.Contracts.Dashboards;

namespace EduTwin.BLL.Dashboards;

public sealed class StudentDashboardResult
{
    public bool IsSuccess { get; private init; }
    public StudentDashboardDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static StudentDashboardResult Success(StudentDashboardDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static StudentDashboardResult ValidationFailed(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed, ErrorMessage = message };

    public static StudentDashboardResult NotFound(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound, ErrorMessage = message };

    public static StudentDashboardResult Forbidden(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource, ErrorMessage = message };
}
