using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin;

public interface IBehaviorTwinUpdater
{
    Task<BehaviorTwin> UpdateAsync(
        Attempt attempt,
        Guid subjectId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
