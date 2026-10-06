using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public class SaveAssignmentDraftResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public int DraftVersion { get; private init; }

    public static SaveAssignmentDraftResult Success(int version = 0) => new() { IsSuccess = true, DraftVersion = version };
    public static SaveAssignmentDraftResult Failure(string errorCode, int currentVersion = 0) => new() { IsSuccess = false, ErrorCode = errorCode, DraftVersion = currentVersion };
}

public interface ISaveAssignmentDraftUseCase
{
    Task<SaveAssignmentDraftResult> ExecuteAsync(
        Guid assignmentId,
        SaveAssignmentDraftRequest request,
        CancellationToken cancellationToken = default);
}
