using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IAssignCurriculumNodesUseCase
{
    Task<AssignCurriculumNodesResult> ExecuteAsync(Guid curriculumId, AssignCurriculumNodesRequest request, CancellationToken cancellationToken = default);
}
