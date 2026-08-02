using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public interface IListAssignmentsUseCase
{
    Task<ListAssignmentsResult> ExecuteAsync(ListAssignmentsQuery query, CancellationToken cancellationToken = default);
}
