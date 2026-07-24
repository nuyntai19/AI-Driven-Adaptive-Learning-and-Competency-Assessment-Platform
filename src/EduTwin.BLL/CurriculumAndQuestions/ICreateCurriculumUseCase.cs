using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface ICreateCurriculumUseCase
{
    Task<CreateCurriculumResult> ExecuteAsync(CreateCurriculumRequest request, CancellationToken cancellationToken = default);
}
