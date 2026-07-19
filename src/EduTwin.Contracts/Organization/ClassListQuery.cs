using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.Organization;

public class ClassListQuery : IValidatableObject
{
    public Guid? TeacherId { get; set; }
    public Guid? SubjectId { get; set; }
    public ClassStatus? Status { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TeacherId == Guid.Empty)
        {
            yield return new ValidationResult("TeacherId cannot be empty Guid.", new[] { nameof(TeacherId) });
        }
        if (SubjectId == Guid.Empty)
        {
            yield return new ValidationResult("SubjectId cannot be empty Guid.", new[] { nameof(SubjectId) });
        }
    }
}
