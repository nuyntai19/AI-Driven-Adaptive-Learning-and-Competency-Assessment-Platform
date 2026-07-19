using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IListClassesUseCase
{
    Task<ListClassesResult> ExecuteAsync(ClassListQuery query, CancellationToken cancellationToken = default);
}
