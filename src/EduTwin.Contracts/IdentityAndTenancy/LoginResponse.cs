using System;
using System.Text.Json.Serialization;

namespace EduTwin.Contracts.IdentityAndTenancy;

public class UserDto
{
    public required string UserId { get; set; }
    public required string CenterId { get; set; }
    public required string CenterName { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; }
}

public class LoginDataDto
{
    public required string AccessToken { get; set; }
    public string TokenType { get; } = "Bearer";
    public int ExpiresInSeconds { get; } = 900;
    public required UserDto User { get; set; }
}

public class MetaDto
{
    public required string TraceId { get; set; }
    public required DateTime Timestamp { get; set; }
}

public class LoginResponse
{
    public required LoginDataDto Data { get; set; }
    public required MetaDto Meta { get; set; }
}
