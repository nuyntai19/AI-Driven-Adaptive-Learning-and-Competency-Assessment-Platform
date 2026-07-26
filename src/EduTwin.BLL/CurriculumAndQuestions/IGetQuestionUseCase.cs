using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IGetQuestionUseCase
{
    Task<GetQuestionResult> ExecuteAsync(string questionId, CancellationToken cancellationToken = default);
}
