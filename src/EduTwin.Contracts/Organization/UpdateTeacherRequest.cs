using System.ComponentModel.DataAnnotations;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class UpdateTeacherRequest
{
    private string _displayName = string.Empty;
    private string? _department;

    [Required(ErrorMessage = "Tên hiển thị là bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên hiển thị không được vượt quá 200 ký tự.")]
    public string DisplayName
    {
        get => _displayName;
        set => _displayName = value?.Trim() ?? string.Empty;
    }

    [StringLength(150, ErrorMessage = "Tên phòng ban không được vượt quá 150 ký tự.")]
    public string? Department
    {
        get => _department;
        set => _department = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [Required(ErrorMessage = "Trạng thái tài khoản là bắt buộc.")]
    [EnumDataType(typeof(UserStatus), ErrorMessage = "Trạng thái không hợp lệ.")]
    public UserStatus Status { get; set; }

    [Required(ErrorMessage = "Phiên bản dữ liệu (RowVersion) là bắt buộc.")]
    public string RowVersion { get; set; } = string.Empty;
}
