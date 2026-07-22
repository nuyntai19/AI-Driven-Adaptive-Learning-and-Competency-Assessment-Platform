using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace EduTwin.Contracts.KnowledgeGraph;

public class UpdateSubjectRequest : IValidatableObject
{
    [Required(ErrorMessage = "Mã môn học là bắt buộc.")]
    [StringLength(32, ErrorMessage = "Mã môn học không được vượt quá 32 ký tự.")]
    public string SubjectCode { get; set; } = null!;

    [Required(ErrorMessage = "Tên môn học là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên môn học không được vượt quá 100 ký tự.")]
    public string SubjectName { get; set; } = null!;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Trạng thái môn học là bắt buộc.")]
    public bool? IsActive { get; set; }

    [Required(ErrorMessage = "Phiên bản là bắt buộc.")]
    [RegularExpression("^[0-9]+$", ErrorMessage = "Phiên bản không hợp lệ.")]
    public string RowVersion { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RowVersion != null)
        {
            // NumberStyles.None prevents leading/trailing whitespaces, signs (+/-), decimals.
            // InvariantCulture prevents localized digits (unless framework defaults allow, but TryParse with None usually rejects non-ASCII digits if string is purely parsed as integer).
            if (!ulong.TryParse(RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version == 0)
            {
                yield return new ValidationResult("Phiên bản không hợp lệ.", new[] { nameof(RowVersion) });
            }
            else
            {
                // Ensure no unicode digits bypassing regex by checking if all chars are ASCII digits
                foreach (var c in RowVersion)
                {
                    if (c < '0' || c > '9')
                    {
                        yield return new ValidationResult("Phiên bản không hợp lệ.", new[] { nameof(RowVersion) });
                        break;
                    }
                }
            }
        }
    }
}
