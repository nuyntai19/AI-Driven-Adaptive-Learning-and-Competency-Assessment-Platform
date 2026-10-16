using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Services;

public sealed record LearningTutorContext(string ActivityType, string Topic, string Content, string ReferenceSolution,
    string StudentResponse, string? Reasoning, bool? IsCorrect);

public interface ILearningTutorFeedbackGenerator
{
    Task<LearningTutorFeedbackDto> GenerateAsync(LearningTutorContext context, CancellationToken cancellationToken);
}
