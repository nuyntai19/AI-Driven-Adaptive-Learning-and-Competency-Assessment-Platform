using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;

namespace EduTwin.BLL.Recommendations.Services;

public interface ILearningPathV4Evaluator
{
    Task<V4SessionEvaluationResultDto> EvaluateMissionRunAsync(
        Guid centerId,
        Guid studentId,
        ulong sessionRunId,
        CancellationToken cancellationToken = default);

    Task<V4SessionEvaluationResultDto> EvaluateLateReviewAsync(
        Guid centerId,
        ulong attemptId,
        decimal overrideScore,
        CancellationToken cancellationToken = default);
}
