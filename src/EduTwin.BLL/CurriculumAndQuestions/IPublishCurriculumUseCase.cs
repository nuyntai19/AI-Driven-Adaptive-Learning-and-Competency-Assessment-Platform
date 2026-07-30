using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public interface IPublishCurriculumUseCase
{
    Task<PublishCurriculumResult> ExecuteAsync(Guid curriculumId, PublishCurriculumRequest request, CancellationToken cancellationToken = default);
}
