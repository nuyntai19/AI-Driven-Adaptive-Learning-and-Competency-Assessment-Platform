using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IUpdateSubjectUseCase
{
    Task<UpdateSubjectResult> ExecuteAsync(Guid subjectId, UpdateSubjectRequest request, CancellationToken cancellationToken);
}
