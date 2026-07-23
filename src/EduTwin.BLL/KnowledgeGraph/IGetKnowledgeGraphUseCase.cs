using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IGetKnowledgeGraphUseCase
{
    Task<GetKnowledgeGraphResult> ExecuteAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default);
}
