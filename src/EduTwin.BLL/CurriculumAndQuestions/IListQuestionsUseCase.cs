using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IListQuestionsUseCase
{
    Task<ListQuestionsResult> ExecuteAsync(QuestionListQuery query, CancellationToken cancellationToken = default);
}
