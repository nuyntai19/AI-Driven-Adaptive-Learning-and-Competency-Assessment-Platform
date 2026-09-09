using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.IdentityAndTenancy;

public sealed class AuthorizationAuditLog : ITenantAppendOnlyEntity
{
    public ulong AuthorizationAuditId { get; set; }
    public Guid CenterId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActionType { get; set; } = null!;
    public string TargetType { get; set; } = null!;
    public string TargetId { get; set; } = null!;
    public Guid? TargetUserId { get; set; }
    public string? PermissionCode { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string Reason { get; set; } = null!;
    public string TraceId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public User? ActorUser { get; set; }
    public User? TargetUser { get; set; }
}
