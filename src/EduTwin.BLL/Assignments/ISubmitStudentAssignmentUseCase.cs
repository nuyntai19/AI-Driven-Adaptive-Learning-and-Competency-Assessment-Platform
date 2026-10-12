using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public class SubmitStudentAssignmentResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public SubmitAssignmentResponseDto? Data { get; private init; }

    public static SubmitStudentAssignmentResult Success(SubmitAssignmentResponseDto data) =>
        new() { IsSuccess = true, Data = data };

    public static SubmitStudentAssignmentResult Failure(string errorCode) =>
        new() { IsSuccess = false, ErrorCode = errorCode };
}

public interface ISubmitStudentAssignmentUseCase
{
    Task<SubmitStudentAssignmentResult> ExecuteAsync(
        Guid assignmentId,
        SubmitAssignmentRequest request,
        CancellationToken cancellationToken = default);
}
