using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IDeleteKnowledgeNodeUseCase
{
    Task<DeleteKnowledgeNodeResult> ExecuteAsync(
        string nodeId,
        CancellationToken cancellationToken = default);
}
