using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IUpdateCurriculumUseCase
{
    Task<UpdateCurriculumResult> ExecuteAsync(Guid curriculumId, UpdateCurriculumRequest request, CancellationToken cancellationToken = default);
}
