using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IListKnowledgeNodesUseCase
{
    Task<ListKnowledgeNodesResult> ExecuteAsync(KnowledgeNodeListQuery query, CancellationToken cancellationToken);
}
