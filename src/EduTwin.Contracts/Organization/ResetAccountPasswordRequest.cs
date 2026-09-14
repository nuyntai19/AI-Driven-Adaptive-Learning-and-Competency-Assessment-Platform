namespace EduTwin.Contracts.Organization;

public class ResetAccountPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
    public string ExpectedUserRowVersion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
