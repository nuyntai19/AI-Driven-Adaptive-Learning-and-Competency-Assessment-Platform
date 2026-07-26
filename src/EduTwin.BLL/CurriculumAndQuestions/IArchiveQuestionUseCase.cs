using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IArchiveQuestionUseCase
{
    Task<ArchiveQuestionResult> ExecuteAsync(string questionId, ArchiveQuestionRequest request, CancellationToken cancellationToken = default);
}
