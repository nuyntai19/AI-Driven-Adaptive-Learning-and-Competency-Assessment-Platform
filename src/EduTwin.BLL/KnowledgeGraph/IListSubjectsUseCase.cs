using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IListSubjectsUseCase
{
    Task<ListSubjectsResult> ExecuteAsync(SubjectListQuery query, CancellationToken cancellationToken);
}
