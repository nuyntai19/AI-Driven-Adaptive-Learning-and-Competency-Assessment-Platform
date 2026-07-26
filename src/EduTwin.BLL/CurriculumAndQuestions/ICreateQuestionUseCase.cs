using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface ICreateQuestionUseCase
{
    Task<CreateQuestionResult> ExecuteAsync(CreateQuestionRequest request, CancellationToken cancellationToken = default);
}
