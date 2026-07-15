using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Organization;

public class Subject : IMutableTenantAggregate
{
    public Guid SubjectId { get; set; }
    public string SubjectCode { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    // IMutableTenantAggregate fields
    public Guid CenterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }
    
    // Navigation property
    public Organization.Center Center { get; set; } = null!;
}
