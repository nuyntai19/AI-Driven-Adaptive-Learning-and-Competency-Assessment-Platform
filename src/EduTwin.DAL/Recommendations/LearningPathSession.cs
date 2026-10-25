using System;
using System.Collections.Generic;
using System.Text.Json;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

public class LearningPathSession : IMutableTenantAggregate
{
    public Guid SessionId { get; set; }
    public Guid CenterId { get; set; }
    public Guid LearningPathId { get; set; }
    public string? LegacySessionId { get; set; }
    public ulong TopicNodeId { get; set; }
    public Guid? ClassId { get; set; }

    public uint PhaseNumber { get; set; }
    public uint WeekNumber { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public uint SessionOrder { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string? Rationale { get; set; }

    public LearningPathSessionStatus Status { get; set; } = LearningPathSessionStatus.NotStarted;

    public JsonDocument? PracticeQuestionIds { get; set; }
    public JsonDocument? CheckpointQuestionIds { get; set; }

    public uint MinimumQuestions { get; set; } = 1;
    public decimal RequiredCorrectRate { get; set; } = 0.70m;
    public string PolicyVersion { get; set; } = "session-policy-v1";

    public ulong? CurrentRunId { get; set; }
    public uint TotalRuns { get; set; }

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
    public Class? Class { get; set; }
    public ICollection<LearningPathSessionRun> Runs { get; set; } = new List<LearningPathSessionRun>();
}
