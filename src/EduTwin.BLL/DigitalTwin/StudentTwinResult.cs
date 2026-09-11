using EduTwin.Contracts.Common;
using EduTwin.Contracts.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin;

public sealed class StudentTwinResult
{
    public bool IsSuccess { get; private init; }
    public StudentTwinDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }

    public static StudentTwinResult Success(StudentTwinDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static StudentTwinResult ValidationFailed() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed };

    public static StudentTwinResult NotFound() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };

    public static StudentTwinResult Forbidden() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource };
}
