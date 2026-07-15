using System;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.Assignments;

public class Assignment : IMutableTenantAggregate
{
    public Guid AssignmentId { get; set; }
    public Guid CenterId { get; set; }
    public Guid ClassId { get; set; }
    public Guid CreatedByTeacherId { get; set; }

    public string Title { get; set; } = null!;
    public string? Instructions { get; set; }
    public DateTime? DueAt { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }

    // Audit and MTA
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    // Navigations
    public Class? Class { get; set; }
    public Teacher? CreatedByTeacher { get; set; }
}
