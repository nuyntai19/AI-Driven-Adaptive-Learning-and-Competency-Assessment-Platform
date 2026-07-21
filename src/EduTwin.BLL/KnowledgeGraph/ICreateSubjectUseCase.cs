using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface ICreateSubjectUseCase
{
    Task<CreateSubjectResult> ExecuteAsync(CreateSubjectRequest request, CancellationToken cancellationToken = default);
}
