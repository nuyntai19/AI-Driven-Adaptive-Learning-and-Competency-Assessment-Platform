using System;
using System.Collections.Generic;
using EduTwin.Contracts.AdaptivePractice;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.AdaptivePractice;

public class AdaptivePracticeSession : IMutableTenantAggregate
{
    public Guid SessionId { get; set; }
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public ulong TopicNodeId { get; set; }
    public Guid? LearningPathId { get; set; }
    public ulong? LearningPathItemId { get; set; }

    public AdaptivePracticeMode PracticeMode { get; set; }
    public decimal StartingMastery { get; set; }
    public decimal TargetMastery { get; set; }
    public decimal CurrentMastery { get; set; }
    public string? TargetSkillIds { get; set; }
    public int TimeBudgetMinutes { get; set; }
    public int MinimumQuestions { get; set; }
    public int MaximumQuestions { get; set; }
    public int QuestionCount { get; set; }

    public AdaptivePracticeSessionStatus Status { get; set; }
    public AdaptivePracticeCloseReason? CloseReason { get; set; }
    public string SelectionReason { get; set; } = null!;
    public string PolicyVersion { get; set; } = "1.0";

    public byte CurrentDifficulty { get; set; } = 2;
    public int ConsecutiveCorrect { get; set; } = 0;
    public int ConsecutiveIncorrect { get; set; } = 0;
    public bool IsDiagnosticMode { get; set; } = false;
    public int DiagnosticQuestionsCount { get; set; } = 0;
    public ulong? PrerequisiteTestedNodeId { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

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
    public Student Student { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public KnowledgeNode TopicNode { get; set; } = null!;
    public LearningPath? LearningPath { get; set; }
    public ICollection<AdaptivePracticeSessionItem> Items { get; set; } = new List<AdaptivePracticeSessionItem>();
    public AdaptivePracticeSessionSummary? Summary { get; set; }
}
