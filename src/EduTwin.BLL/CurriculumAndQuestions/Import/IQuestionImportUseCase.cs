using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

public interface IQuestionImportUseCase
{
    Task<QuestionImportPreviewResult> PreviewAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken);

    Task<QuestionImportConfirmResult> ConfirmAsync(
        QuestionImportConfirmRequest request,
        CancellationToken cancellationToken);
}

public sealed class QuestionImportPreviewResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public QuestionImportPreviewDataDto? Data { get; private init; }

    public static QuestionImportPreviewResult Success(QuestionImportPreviewDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static QuestionImportPreviewResult Failure(string errorCode, string errorMessage) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

    public static QuestionImportPreviewResult ValidationFailed(string message) =>
        Failure("VALIDATION_FAILED", message);
}

public sealed class QuestionImportConfirmResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public QuestionImportConfirmDataDto? Data { get; private init; }

    public static QuestionImportConfirmResult Success(QuestionImportConfirmDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static QuestionImportConfirmResult Failure(string errorCode, string errorMessage) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

    public static QuestionImportConfirmResult ValidationFailed(string message) =>
        Failure("VALIDATION_FAILED", message);

    public static QuestionImportConfirmResult NotFound(string message) =>
        Failure("NOT_FOUND", message);
}
