using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IListTeachersUseCase
{
    Task<ListTeachersResult> ExecuteAsync(TeacherListQuery query, CancellationToken cancellationToken = default);
}
