using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace EduTwin.Contracts.Organization;

public class CreateStudentRequest : IValidatableObject
{
    private string _username = string.Empty;
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên đăng nhập không được vượt quá 100 ký tự.")]
    public string Username
    {
        get => _username;
        set => _username = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Mật khẩu tạm thời là bắt buộc.")]
    [StringLength(200, MinimumLength = 12, ErrorMessage = "Mật khẩu phải từ 12 đến 200 ký tự.")]
    public string TemporaryPassword { get; set; } = string.Empty;

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

    public List<Guid>? ClassIds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ClassIds == null)
        {
            yield return new ValidationResult("Danh sách lớp học không hợp lệ.", new[] { nameof(ClassIds) });
            yield break;
        }

        if (ClassIds.Any(id => id == Guid.Empty))
        {
            yield return new ValidationResult("Mã lớp học không được rỗng.", new[] { nameof(ClassIds) });
        }

        if (ClassIds.Distinct().Count() != ClassIds.Count)
        {
            yield return new ValidationResult("Danh sách lớp học không được chứa giá trị trùng lặp.", new[] { nameof(ClassIds) });
        }
    }
}
