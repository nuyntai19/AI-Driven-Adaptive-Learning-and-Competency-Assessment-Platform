using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.IdentityAndTenancy;

/// <summary>
/// Sign-in for the platform administrator portal (/admin). The platform tenant is implied,
/// so the request carries no center code.
/// </summary>
public class AdminLoginRequest
{
    private string _username = string.Empty;

    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên đăng nhập không được vượt quá 100 ký tự.")]
    public required string Username
    {
        get => _username;
        set => _username = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    public required string Password { get; set; }
}
