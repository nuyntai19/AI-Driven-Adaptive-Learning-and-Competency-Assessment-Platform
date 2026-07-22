using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IDeleteSubjectUseCase
{
    Task<DeleteSubjectResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken = default);
}
