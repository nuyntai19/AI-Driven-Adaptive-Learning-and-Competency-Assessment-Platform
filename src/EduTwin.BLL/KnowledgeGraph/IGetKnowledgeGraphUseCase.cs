using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IGetKnowledgeGraphUseCase
{
    Task<GetKnowledgeGraphResult> ExecuteAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default);
    Task<GetKnowledgeGraphResult> ExecuteAsync(KnowledgeGraphQuery query, CancellationToken cancellationToken = default)
        => ExecuteAsync(query.SubjectId, cancellationToken);
}
