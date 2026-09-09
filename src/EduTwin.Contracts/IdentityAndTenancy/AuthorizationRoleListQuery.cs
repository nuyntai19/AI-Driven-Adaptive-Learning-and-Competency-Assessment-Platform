namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AuthorizationRoleListQuery
{
    public string? Search { get; set; }
    public UserRole? AccountType { get; set; }
    public AuthorizationRoleStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
