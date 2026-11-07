using System;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Organization;

public class TeacherStudentNote : IMutableTenantAggregate
{
    public Guid NoteId { get; set; }
    public Guid CenterId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid StudentId { get; set; }
    public string Content { get; set; } = string.Empty;

    // IMutableTenantAggregate fields
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
    public Student Student { get; set; } = null!;
}
