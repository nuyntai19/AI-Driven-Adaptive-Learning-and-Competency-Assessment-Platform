using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.Organization;

public class UpsertStudentSubjectGoalRequest : IValidatableObject
{
    [Range(0.0, 10.0, ErrorMessage = "TargetScore must be between 0 and 10.")]
    public decimal TargetScore { get; set; }

    [Range(0, 3650, ErrorMessage = "RemainingDays must be between 0 and 3650.")]
    public int RemainingDays { get; set; }

    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        int[] bits = decimal.GetBits(TargetScore);
        int scale = (bits[3] >> 16) & 31;
        if (scale > 2)
        {
            yield return new ValidationResult("TargetScore cannot have more than 2 decimal places.", new[] { nameof(TargetScore) });
        }

        if (RowVersion != null)
        {
            if (string.IsNullOrEmpty(RowVersion) || RowVersion.Length == 0)
            {
                yield return new ValidationResult("RowVersion must not be empty.", new[] { nameof(RowVersion) });
            }
            else
            {
                foreach (char c in RowVersion)
                {
                    if (!char.IsAsciiDigit(c))
                    {
                        yield return new ValidationResult("RowVersion must contain only ASCII digits.", new[] { nameof(RowVersion) });
                        break;
                    }
                }

                if (!ulong.TryParse(RowVersion, out var version) || version == 0)
                {
                    yield return new ValidationResult("RowVersion must be greater than 0.", new[] { nameof(RowVersion) });
                }
            }
        }
    }
}
