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
        Guid subjectId,
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

    public static TwinHistoryResult Success(List<TwinHistoryItemDto> data) =>
        new() { IsSuccess = true, Data = data };

    public static TwinHistoryResult ValidationFailed() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed };

    public static TwinHistoryResult NotFound() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };

    public static TwinHistoryResult Forbidden() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource };
}
