using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Organization;

public class Student : IMutableTenantAggregate
{
    public Guid StudentId { get; set; }
    public string FullName { get; set; } = null!;
    public byte GradeLevel { get; set; }
    public DateOnly? DateOfBirth { get; set; }

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
