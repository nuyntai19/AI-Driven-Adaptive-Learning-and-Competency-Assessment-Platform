namespace EduTwin.BLL.Recommendations.Services;

public sealed class LearningMissionInProgressException(Guid missionId, ulong runId, string missionTitle)
    : InvalidOperationException($"Bạn đang học dở “{missionTitle}” trong môn này. Hãy tiếp tục buổi đó trước khi mở buổi mới.")
{
    public Guid MissionId { get; } = missionId;
    public ulong RunId { get; } = runId;
    public string MissionTitle { get; } = missionTitle;
}
