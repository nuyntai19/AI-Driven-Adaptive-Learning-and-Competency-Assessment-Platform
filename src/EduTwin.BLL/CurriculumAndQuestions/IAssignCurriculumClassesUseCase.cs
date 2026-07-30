using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IAssignCurriculumClassesUseCase
{
    Task<AssignCurriculumClassesResult> ExecuteAsync(Guid curriculumId, AssignCurriculumClassesRequest request, CancellationToken cancellationToken = default);
}
