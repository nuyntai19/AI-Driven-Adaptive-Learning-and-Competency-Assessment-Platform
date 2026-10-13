using System;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.Organization;

public class CreateClassRequest
{
    [Required]
    [MaxLength(150)]
    public string ClassName { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    public string AcademicYear { get; set; } = null!;

    public Guid SubjectId { get; set; }

    public Guid TeacherId { get; set; }

    [Required]
    [Range(10, 12)]
    public byte? GradeLevel { get; set; }
}
