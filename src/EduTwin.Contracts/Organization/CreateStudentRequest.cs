using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace EduTwin.Contracts.Organization;

public class CreateStudentRequest : IValidatableObject
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, ErrorMessage = "Username cannot exceed 100 characters.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Temporary password is required.")]
    [StringLength(200, MinimumLength = 12, ErrorMessage = "Password must be between 12 and 200 characters.")]
    public string TemporaryPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(200, ErrorMessage = "Full name cannot exceed 200 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Grade level is required.")]
    [Range(10, 12, ErrorMessage = "Grade level must be 10, 11, or 12.")]
    public int GradeLevel { get; set; }

    [Required(ErrorMessage = "ClassIds is required.")]
    public List<Guid> ClassIds { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClassIds == null)
        {
            yield return new ValidationResult("ClassIds cannot be null.", new[] { nameof(ClassIds) });
            yield break;
        }

        if (ClassIds.Any(id => id == Guid.Empty))
        {
            yield return new ValidationResult("ClassIds cannot contain empty GUIDs.", new[] { nameof(ClassIds) });
        }

        if (ClassIds.Distinct().Count() != ClassIds.Count)
        {
            yield return new ValidationResult("ClassIds cannot contain duplicates.", new[] { nameof(ClassIds) });
        }
    }
}
