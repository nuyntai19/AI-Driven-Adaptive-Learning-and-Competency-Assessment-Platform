using System;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.Assignments;

public class StudentAssignmentProgress : IMutableTenantAggregate
{
    public ulong ProgressId { get; set; }
    public Guid CenterId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    
    public ProgressStatus Status { get; set; }
    public uint CompletedQuestionCount { get; set; }
    public uint TotalQuestionCount { get; set; }
    
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

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
    public Assignment? Assignment { get; set; }
    public Student? Student { get; set; }
}
