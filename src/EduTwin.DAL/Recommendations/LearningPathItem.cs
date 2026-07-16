using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.DAL.Recommendations;

public class LearningPathItem : IMutableTenantAggregate
{
    public ulong LearningPathItemId { get; set; }
    public Guid LearningPathId { get; set; }
    public ulong TopicNodeId { get; set; }
    public ulong? RecommendedQuestionId { get; set; }
    public uint RankOrder { get; set; }
    public decimal? OpportunityScore { get; set; }
    public string Reason { get; set; } = null!;
    public LearningPathItemStatus Status { get; set; }

    public Guid CenterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    public LearningPath LearningPath { get; set; } = null!;
    public KnowledgeNode TopicNode { get; set; } = null!;
    public Question? RecommendedQuestion { get; set; }
}
