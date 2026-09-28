using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IArchiveCurriculumUseCase
{
    Task<ArchiveCurriculumResult> ExecuteAsync(Guid curriculumId, ArchiveCurriculumRequest request, CancellationToken cancellationToken = default);
}
