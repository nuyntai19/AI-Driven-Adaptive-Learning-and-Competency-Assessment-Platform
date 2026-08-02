using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public interface ICreateAssignmentUseCase
{
    Task<CreateAssignmentResult> ExecuteAsync(CreateAssignmentRequest request, CancellationToken cancellationToken = default);
}
