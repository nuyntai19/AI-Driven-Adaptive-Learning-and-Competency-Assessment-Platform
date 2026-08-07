using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.Assignments;

public interface IListStudentAssignmentsUseCase
{
    Task<ListStudentAssignmentsResult> ExecuteAsync(ListStudentAssignmentsQuery query, CancellationToken cancellationToken);
}
