using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Organization;

public class Teacher : IMutableTenantAggregate
{
    public Guid TeacherId { get; set; }
    public string? Department { get; set; }
    public string? Bio { get; set; }

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
    public IdentityAndTenancy.User User { get; set; } = null!;
}
