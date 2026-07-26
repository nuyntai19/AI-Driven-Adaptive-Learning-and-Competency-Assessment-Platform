using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IDeleteQuestionUseCase
{
    Task<DeleteQuestionResult> ExecuteAsync(string questionId, CancellationToken cancellationToken = default);
}
