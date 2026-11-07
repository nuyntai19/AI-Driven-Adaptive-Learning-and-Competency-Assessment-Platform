using System;
using EduTwin.Contracts.AdaptivePractice;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AdaptivePractice;

public class AdaptivePracticeSessionItem : ITenantAppendOnlyEntity
{
    public ulong SessionItemId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SessionId { get; set; }
    public ulong QuestionId { get; set; }
    public ulong KnowledgeNodeId { get; set; }

    public byte Difficulty { get; set; }
    public int SequenceNumber { get; set; }
    public string SelectionReasonCode { get; set; } = null!;
    public string SelectionReasonText { get; set; } = null!;
    public string QuestionVersion { get; set; } = "1.0";

    public DateTime PresentedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public AdaptiveSessionItemStatus Status { get; set; } = AdaptiveSessionItemStatus.Presented;
    public ulong? LastAttemptId { get; set; }
    public int HintsUsedCount { get; set; } = 0;

    // ITenantAppendOnlyEntity audit fields
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    // Navigations
    public AdaptivePracticeSession Session { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public KnowledgeNode KnowledgeNode { get; set; } = null!;
}
