using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin;

public interface IGetStudentTwinHistoryUseCase
{
    Task<TwinHistoryResult> ExecuteAsync(
        Guid? subjectId,
        ulong? topicId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken);
}

public sealed class TwinHistoryResult
{
    public bool IsSuccess { get; private init; }
    public List<TwinHistoryItemDto>? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static TwinHistoryResult Success(List<TwinHistoryItemDto> data) =>
        new() { IsSuccess = true, Data = data };

    public static TwinHistoryResult ValidationFailed(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed, ErrorMessage = message };

    public static TwinHistoryResult NotFound(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound, ErrorMessage = message };

    public static TwinHistoryResult Forbidden(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource, ErrorMessage = message };
}
