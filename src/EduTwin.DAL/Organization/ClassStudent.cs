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

    // ITenantJoinEntity fields
    public Guid CenterId { get; set; }
    public Guid? CreatedBy { get; set; }

    // Navigation properties
    public Class Class { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
