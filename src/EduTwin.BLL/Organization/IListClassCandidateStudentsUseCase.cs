using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IListClassCandidateStudentsUseCase
{
    Task<ListStudentsResult> ExecuteAsync(Guid classId, CandidateStudentListQuery query, CancellationToken cancellationToken);
}
