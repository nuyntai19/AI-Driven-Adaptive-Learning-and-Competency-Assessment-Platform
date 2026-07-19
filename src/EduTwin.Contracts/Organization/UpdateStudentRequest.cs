using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class UpdateStudentRequest : IValidatableObject
{
    private string _fullName = string.Empty;
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Họ tên không được vượt quá 200 ký tự.")]
    public string FullName
    {
        get => _fullName;
        set => _fullName = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Khối lớp là bắt buộc.")]
    [Range(10, 12, ErrorMessage = "Khối lớp phải là 10, 11 hoặc 12.")]
    public int GradeLevel { get; set; }

    [Required(ErrorMessage = "Trạng thái là bắt buộc.")]
    [EnumDataType(typeof(UserStatus), ErrorMessage = "Trạng thái không hợp lệ.")]
    public UserStatus? Status { get; set; }

    [Required(ErrorMessage = "RowVersion là bắt buộc.")]
    public string RowVersion { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            yield return new ValidationResult("Họ tên không được để trống.", new[] { nameof(FullName) });
        }

        if (string.IsNullOrEmpty(RowVersion) || !Regex.IsMatch(RowVersion, "^[1-9][0-9]*$"))
        {
            yield return new ValidationResult("RowVersion không hợp lệ.", new[] { nameof(RowVersion) });
        }
        else if (!ulong.TryParse(RowVersion, out _))
        {
            yield return new ValidationResult("RowVersion vượt quá giới hạn.", new[] { nameof(RowVersion) });
        }
    }
}
