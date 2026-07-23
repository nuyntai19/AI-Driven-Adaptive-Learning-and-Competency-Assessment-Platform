using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace EduTwin.Contracts.KnowledgeGraph;

public class UpdateKnowledgeEdgeRequest : IValidatableObject
{
    [Required(ErrorMessage = "Trọng số là bắt buộc.")]
    [Range(typeof(decimal), "0", "1", ErrorMessage = "Trọng số phải nằm trong khoảng từ 0 đến 1.")]
    public decimal? Weight { get; set; }

    [Required(ErrorMessage = "Phiên bản là bắt buộc.")]
    [RegularExpression("^[0-9]+$", ErrorMessage = "Phiên bản không hợp lệ.")]
    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(RowVersion))
        {
            if (!ulong.TryParse(RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed == 0)
            {
                yield return new ValidationResult(
                    "Phiên bản không hợp lệ.",
                    new[] { nameof(RowVersion) });
            }
        }
    }
}
