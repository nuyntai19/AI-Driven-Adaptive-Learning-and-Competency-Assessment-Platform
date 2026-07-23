using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IUpdateKnowledgeEdgeUseCase
{
    Task<UpdateKnowledgeEdgeResult> ExecuteAsync(
        string edgeId,
        UpdateKnowledgeEdgeRequest request,
        CancellationToken cancellationToken = default);
}
