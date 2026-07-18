using EduTwin.Contracts.IdentityAndTenancy;
using System;

namespace EduTwin.BLL.IdentityAndTenancy;

public class LoginResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public LoginDataDto? Data { get; set; }
    public string? RawRefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
}
