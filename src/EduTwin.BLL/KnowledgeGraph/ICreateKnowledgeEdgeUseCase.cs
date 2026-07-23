using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface ICreateKnowledgeEdgeUseCase
{
    Task<CreateKnowledgeEdgeResult> ExecuteAsync(CreateKnowledgeEdgeRequest request, CancellationToken cancellationToken);
}
