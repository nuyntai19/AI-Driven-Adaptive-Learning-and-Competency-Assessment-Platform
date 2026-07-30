using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IGetCurriculumUseCase
{
    Task<GetCurriculumResult> ExecuteAsync(GetCurriculumRequest request, CancellationToken cancellationToken = default);
}
