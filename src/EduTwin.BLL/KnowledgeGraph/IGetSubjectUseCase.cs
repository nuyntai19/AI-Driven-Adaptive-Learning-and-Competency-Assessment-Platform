using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IGetSubjectUseCase
{
    Task<GetSubjectResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken);
}
