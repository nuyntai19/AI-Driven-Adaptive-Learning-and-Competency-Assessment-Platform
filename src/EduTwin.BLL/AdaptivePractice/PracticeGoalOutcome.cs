using EduTwin.Contracts.AdaptivePractice;

namespace EduTwin.BLL.AdaptivePractice;

/// <summary>Ending a session does not establish achievement of its learning goal.</summary>
internal static class PracticeGoalOutcome
{
    public static string Classify(AdaptivePracticeCloseReason? reason, int answered, decimal startingMastery, decimal? endingMastery) => reason switch
    {
        AdaptivePracticeCloseReason.GoalAchievedWithEvidence => "Achieved",
        AdaptivePracticeCloseReason.RemediationNeeded => "RemediationRecommended",
        _ when answered > 0 && endingMastery > startingMastery => "PartiallyAchieved",
        _ => "NotAchieved"
    };
}
