using System;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.Organization;

public class UpdateClassRequest
{
    [Required]
    [MaxLength(150)]
    public string ClassName { get; init; } = null!;

    public Guid TeacherId { get; init; }

    [Required]
    [EnumDataType(typeof(ClassStatus))]
    public ClassStatus? Status { get; init; }

    [Required]
    public string RowVersion { get; init; } = null!;
}
