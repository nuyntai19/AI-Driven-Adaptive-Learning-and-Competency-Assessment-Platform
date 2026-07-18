using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.IdentityAndTenancy;

public class LoginRequest
{
    private string _centerCode = string.Empty;
    private string _username = string.Empty;

    [Required(ErrorMessage = "Mã trung tâm là bắt buộc.")]
    [StringLength(32, ErrorMessage = "Mã trung tâm không được vượt quá 32 ký tự.")]
    public required string CenterCode
    {
        get => _centerCode;
        set => _centerCode = value?.Trim() ?? string.Empty;
    }

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
