namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AuthorizationAuditQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string? ActionType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
