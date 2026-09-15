namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AuthorizationUserDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole AccountType { get; set; }
    public UserStatus Status { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public uint AuthVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}
