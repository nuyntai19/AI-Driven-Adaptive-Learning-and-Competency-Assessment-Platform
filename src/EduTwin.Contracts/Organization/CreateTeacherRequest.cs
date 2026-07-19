using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.Organization;

public class CreateTeacherRequest
{
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string? _department;

    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên đăng nhập không được vượt quá 100 ký tự.")]
    public string Username
    {
        get => _username;
        set => _username = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Mật khẩu tạm thời là bắt buộc.")]
    [MinLength(12, ErrorMessage = "Mật khẩu tạm thời phải từ 12 ký tự trở lên.")]
    [MaxLength(200, ErrorMessage = "Mật khẩu tạm thời không được vượt quá 200 ký tự.")]
    public string TemporaryPassword { get; set; } = string.Empty;

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
}
