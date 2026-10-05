using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public class SaveAssignmentDraftResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }

    public static SaveAssignmentDraftResult Success() => new() { IsSuccess = true };
    public static SaveAssignmentDraftResult Failure(string errorCode) => new() { IsSuccess = false, ErrorCode = errorCode };
}

public interface ISaveAssignmentDraftUseCase
{
    Task<SaveAssignmentDraftResult> ExecuteAsync(
        Guid assignmentId,
        SaveAssignmentDraftRequest request,
        CancellationToken cancellationToken = default);
}
