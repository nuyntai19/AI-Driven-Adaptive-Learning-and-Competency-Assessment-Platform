using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IUpdateKnowledgeNodeUseCase
{
    Task<UpdateKnowledgeNodeResult> ExecuteAsync(string nodeId, UpdateKnowledgeNodeRequest request, CancellationToken cancellationToken = default);
}
