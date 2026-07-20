using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace EduTwin.Contracts.Organization;

public class AddStudentsToClassRequest : IValidatableObject
{
    [Required]
    public IReadOnlyCollection<Guid> StudentIds { get; init; } = Array.Empty<Guid>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StudentIds == null || StudentIds.Count == 0)
        {
            yield return new ValidationResult("StudentIds cannot be null or empty.", new[] { nameof(StudentIds) });
            yield break;
        }

        if (StudentIds.Any(id => id == Guid.Empty))
        {
            yield return new ValidationResult("StudentIds cannot contain empty GUIDs.", new[] { nameof(StudentIds) });
        }

        if (StudentIds.Distinct().Count() != StudentIds.Count)
        {
            yield return new ValidationResult("StudentIds cannot contain duplicate IDs.", new[] { nameof(StudentIds) });
        }
    }
}
