using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Organization;

public class ClassStudent : ITenantJoinEntity
{
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime JoinedAt { get; set; }
    public ClassStudentStatus Status { get; set; }
    public DateTime? RemovedAt { get; set; }
    public byte? GradeLevelAtEnrollment { get; set; }
    public string? GradeMismatchReason { get; set; }
    public Guid? ExceptionApprovedBy { get; set; }
    public DateTime? ExceptionApprovedAt { get; set; }

    // ITenantJoinEntity fields
    public Guid CenterId { get; set; }
    public Guid? CreatedBy { get; set; }

    // Navigation properties
    public Class Class { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
