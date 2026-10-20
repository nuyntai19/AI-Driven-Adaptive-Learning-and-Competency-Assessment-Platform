using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

public interface IQuestionImportV2UseCase
{
    Task<QuestionImportPreviewResult> PreviewV2Async(Stream stream, string fileName, QuestionImportV2PreviewRequest request, CancellationToken cancellationToken);
    Task<QuestionImportConfirmResult> ConfirmV2Async(QuestionImportConfirmRequest request, CancellationToken cancellationToken);
    Task<QuestionImportPreviewImageResult?> GetPreviewImageAsync(string previewToken, int revision, string assetId, CancellationToken cancellationToken);
    Task<bool> DiscardSessionAsync(string previewToken, int revision, CancellationToken cancellationToken);
}

public sealed record QuestionImportPreviewImageResult(byte[] Data, string Sha256);
