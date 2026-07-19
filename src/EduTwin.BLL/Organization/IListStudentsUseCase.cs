using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IListStudentsUseCase
{
    Task<ListStudentsResult> ExecuteAsync(StudentListQuery query, CancellationToken cancellationToken = default);
}
