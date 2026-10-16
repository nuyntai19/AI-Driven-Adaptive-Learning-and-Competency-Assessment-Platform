namespace EduTwin.BLL.Recommendations.Services;

public sealed record BlockingLearningMission(Guid MissionId, ulong RunId, string Title, string Status, bool CanResume);

public sealed class LearningPathChangeBlockedException(IReadOnlyList<BlockingLearningMission> missions)
    : InvalidOperationException("Chưa thể chọn tuyến mới vì còn buổi đang làm hoặc chờ xử lý trong môn này. Tuyến hiện tại và bài làm vẫn được giữ.")
{
    public IReadOnlyList<BlockingLearningMission> Missions { get; } = missions;
}
