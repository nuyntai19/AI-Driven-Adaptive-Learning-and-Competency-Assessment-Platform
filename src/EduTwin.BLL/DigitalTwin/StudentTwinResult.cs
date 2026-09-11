using EduTwin.Contracts.Common;
using EduTwin.Contracts.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin;

public sealed class StudentTwinResult
{
    public bool IsSuccess { get; private init; }
    public StudentTwinDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static StudentTwinResult Success(StudentTwinDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static StudentTwinResult ValidationFailed(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed, ErrorMessage = message };

    public static StudentTwinResult NotFound(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound, ErrorMessage = message };

    public static StudentTwinResult Forbidden(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource, ErrorMessage = message };
}
