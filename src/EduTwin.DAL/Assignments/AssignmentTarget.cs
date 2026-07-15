using System;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.Assignments;

public class AssignmentTarget : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }

    public TargetSource TargetSource { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }

    // Navigations
    public Assignment? Assignment { get; set; }
    public Student? Student { get; set; }
}
