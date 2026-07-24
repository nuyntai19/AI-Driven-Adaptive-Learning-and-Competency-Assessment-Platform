using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IListCurriculumsUseCase
{
    Task<ListCurriculumsResult> ExecuteAsync(CurriculumListQuery query, CancellationToken cancellationToken = default);
}
