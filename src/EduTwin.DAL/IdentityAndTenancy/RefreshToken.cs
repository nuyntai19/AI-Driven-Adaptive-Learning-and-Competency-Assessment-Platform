using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.IdentityAndTenancy;

public class RefreshToken : ITenantAppendOnlyEntity
{
    public ulong RefreshTokenId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public ulong? ReplacedByTokenId { get; set; }
    public string? RevokeReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    // ITenantAppendOnlyEntity fields
    public Guid CenterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public RefreshToken? ReplacedByToken { get; set; }
}
