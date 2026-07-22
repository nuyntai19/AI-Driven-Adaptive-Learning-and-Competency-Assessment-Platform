using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface ICreateKnowledgeNodeUseCase
{
    Task<CreateKnowledgeNodeResult> ExecuteAsync(CreateKnowledgeNodeRequest request, CancellationToken cancellationToken = default);
}
