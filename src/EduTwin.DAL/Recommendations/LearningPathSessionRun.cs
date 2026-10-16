using System;
using System.Collections.Generic;
using System.Text.Json;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

public class LearningPathSessionRun : IMutableTenantAggregate
{
    public ulong SessionRunId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }

    public uint RunNumber { get; set; } = 1;
    public SessionRunStatus Status { get; set; } = SessionRunStatus.InProgress;
    public SessionRunEvaluationStatus EvaluationStatus { get; set; } = SessionRunEvaluationStatus.NotStarted;
    public SessionRunOutcome Outcome { get; set; } = SessionRunOutcome.None;

    public int? ActiveKey { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ulong? CurrentQuestionId { get; set; }
    public ulong? CheckpointQuestionId { get; set; }

    public JsonDocument? QuestionsAssigned { get; set; }
    public JsonDocument? PolicySnapshot { get; set; }

    public uint TotalQuestions { get; set; }
    public uint QuestionsAttempted { get; set; }
    public uint QuestionsCorrect { get; set; }
    public decimal? ScoreRate { get; set; }

    public decimal? StartingMastery { get; set; }
    public decimal? EndingMastery { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    public LearningPathSession Session { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public Question? CurrentQuestion { get; set; }
    public Question? CheckpointQuestion { get; set; }
    public ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
}
