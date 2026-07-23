using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IDeleteKnowledgeEdgeUseCase
{
    Task<DeleteKnowledgeEdgeResult> ExecuteAsync(
        string edgeId,
        CancellationToken cancellationToken = default);
}
