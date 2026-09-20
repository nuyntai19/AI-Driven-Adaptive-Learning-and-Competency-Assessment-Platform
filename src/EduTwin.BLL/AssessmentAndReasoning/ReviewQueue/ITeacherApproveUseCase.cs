using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public interface ITeacherApproveUseCase
{
    Task<TeacherApproveResult> ExecuteAsync(
        ulong analysisId,
        TeacherApproveRequest request,
        CancellationToken cancellationToken);
}
