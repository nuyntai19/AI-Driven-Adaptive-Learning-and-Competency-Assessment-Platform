using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.IdentityAndTenancy;

public class User : IMutableTenantAggregate
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole RoleName { get; set; }
    public string DisplayName { get; set; } = null!;
    public UserStatus Status { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public uint AuthVersion { get; set; }

    // IMutableTenantAggregate fields
    public Guid CenterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }
    
    // Navigation property for composite FK reference validation (only required by EF)
    public Organization.Center Center { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
