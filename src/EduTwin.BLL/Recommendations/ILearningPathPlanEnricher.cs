using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Recommendations;

public sealed record LearningPathEnrichmentSessionInput(
    string SessionId,
    string TopicName,
    string Type,
    string Objective,
    IReadOnlyList<string> OnlineActivities,
    IReadOnlyList<string> OfflineActivities);

public sealed record LearningPathEnrichmentRequest(
    string SubjectName,
    string GoalType,
    int TargetWeeks,
    int MinutesPerDay,
    int DaysPerWeek,
    IReadOnlyList<LearningPathEnrichmentSessionInput> Sessions,
    string DeterministicRationale);

public sealed record LearningPathEnrichmentSessionOutput(
    string SessionId,
    string Objective,
    IReadOnlyList<string> OnlineActivities,
    IReadOnlyList<string> OfflineActivities);

public sealed record LearningPathEnrichmentResult(
    string Summary,
    IReadOnlyList<LearningPathEnrichmentSessionOutput> Sessions);

public interface ILearningPathPlanEnricher
{
    Task<LearningPathEnrichmentResult?> EnrichAsync(LearningPathEnrichmentRequest request, CancellationToken cancellationToken);
}
