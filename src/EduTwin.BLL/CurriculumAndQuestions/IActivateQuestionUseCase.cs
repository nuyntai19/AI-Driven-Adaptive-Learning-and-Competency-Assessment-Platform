using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IActivateQuestionUseCase
{
    Task<ActivateQuestionResult> ExecuteAsync(string questionId, ActivateQuestionRequest request, CancellationToken cancellationToken = default);
}
