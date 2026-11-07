using System;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AdaptivePractice;

public class AdaptivePracticeSessionSummary : IMutableTenantAggregate
{
    public Guid SessionId { get; set; }
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }

    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int PartialCount { get; set; }
    public int PendingCount { get; set; }
    public int TimeSpentSeconds { get; set; }

    public decimal StartingMastery { get; set; }
    public decimal? EndingMastery { get; set; }
    public string GoalAchievementStatus { get; set; } = null!;

    public string? StrongSkillIds { get; set; }
    public string? WeakSkillIds { get; set; }
    public string? RemediationSkillIds { get; set; }

    public string SummaryConclusion { get; set; } = null!;
    public string RecommendedNextAction { get; set; } = null!;
    public ulong? RecommendedNextTopicId { get; set; }
    public string SummaryStatus { get; set; } = "Finalized";

    // IMutableTenantAggregate audit fields
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    // Navigations
    public AdaptivePracticeSession Session { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public KnowledgeNode? RecommendedNextTopic { get; set; }
}
