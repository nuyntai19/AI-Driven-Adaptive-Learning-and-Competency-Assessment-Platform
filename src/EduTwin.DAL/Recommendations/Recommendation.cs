using System;
using System.Text.Json;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.DAL.Recommendations;

public class Recommendation : IMutableTenantAggregate
{
    public ulong RecommendationId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public ulong TopicNodeId { get; set; }
    public ulong? QuestionId { get; set; }
    public RecommendationType RecommendationType { get; set; }
    public decimal? OpportunityScore { get; set; }
    public string CalculationVersion { get; set; } = null!;
    public JsonDocument CalculationBreakdown { get; set; } = null!;
    public string Explanation { get; set; } = null!;
    public ulong? SourceAttemptId { get; set; }
    public RecommendationStatus Status { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public Guid CenterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    public Student Student { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public KnowledgeNode TopicNode { get; set; } = null!;
    public Question? Question { get; set; }
}
