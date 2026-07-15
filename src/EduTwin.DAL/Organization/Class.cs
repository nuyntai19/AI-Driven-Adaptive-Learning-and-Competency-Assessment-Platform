using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Organization;

public class Class : IMutableTenantAggregate
{
    public Guid ClassId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid SubjectId { get; set; }
    public string ClassName { get; set; } = null!;
    public string AcademicYear { get; set; } = null!;
    public ClassStatus Status { get; set; }

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
    
    // Navigation properties
    public Teacher Teacher { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();
}
