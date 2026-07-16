using System;
using System.Collections.Generic;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.DAL.Recommendations;

public class LearningPath : IMutableTenantAggregate
{
    public Guid LearningPathId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public LearningPathStrategy Strategy { get; set; }
    public uint Version { get; set; }
    public LearningPathStatus Status { get; set; }
    public ulong? GeneratedFromAttemptId { get; set; }
    public DateTime GeneratedAt { get; set; }

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
    public ICollection<LearningPathItem> Items { get; set; } = new List<LearningPathItem>();
}
