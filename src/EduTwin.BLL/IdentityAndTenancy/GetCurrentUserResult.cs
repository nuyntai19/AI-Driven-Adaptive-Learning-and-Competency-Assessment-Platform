using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public class GetCurrentUserResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public CurrentUserDataDto? Data { get; set; }
}
