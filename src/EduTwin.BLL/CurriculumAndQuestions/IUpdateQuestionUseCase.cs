using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IUpdateQuestionUseCase
{
    Task<UpdateQuestionResult> ExecuteAsync(string questionId, UpdateQuestionRequest request, CancellationToken cancellationToken = default);
}
