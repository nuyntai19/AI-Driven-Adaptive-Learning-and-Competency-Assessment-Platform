using System;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.Platform;

public sealed class PlatformSecurityProfileDto
{
    public required Guid UserId { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string RoleName { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public uint AuthVersion { get; set; }
    public required string RowVersion { get; set; }
    public int ActiveSessionCount { get; set; }
}

public sealed class PlatformChangePasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(12, ErrorMessage = "Mật khẩu mới phải có tối thiểu 12 ký tự.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu mới là bắt buộc.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp với mật khẩu mới.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class PlatformRevokeSessionsRequest
{
    [MaxLength(500, ErrorMessage = "Lý do thu hồi không được vượt quá 500 ký tự.")]
    public string? Reason { get; set; }
}
