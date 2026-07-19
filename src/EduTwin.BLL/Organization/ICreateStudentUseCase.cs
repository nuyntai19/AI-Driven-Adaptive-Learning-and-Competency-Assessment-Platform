using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface ICreateStudentUseCase
{
    Task<CreateStudentResult> ExecuteAsync(CreateStudentRequest request, CancellationToken cancellationToken = default);
}
