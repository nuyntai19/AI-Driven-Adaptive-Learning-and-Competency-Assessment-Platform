using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Override;

public interface ITeacherOverrideUseCase
{
    Task<TeacherOverrideResult> ExecuteAsync(
        ulong analysisId,
        TeacherOverrideRequest request,
        CancellationToken cancellationToken);
}
