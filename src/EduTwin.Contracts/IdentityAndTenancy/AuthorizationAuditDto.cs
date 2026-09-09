using System.Text.Json;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AuthorizationAuditDto
{
    public ulong AuthorizationAuditId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public Guid? TargetUserId { get; set; }
    public string? PermissionCode { get; set; }
    public JsonElement? Before { get; set; }
    public JsonElement? After { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
