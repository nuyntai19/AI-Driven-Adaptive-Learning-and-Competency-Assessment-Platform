using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface ICloneCurriculumUseCase
{
    Task<CloneCurriculumResult> ExecuteAsync(Guid curriculumId, CloneCurriculumRequest request, CancellationToken cancellationToken = default);
}
