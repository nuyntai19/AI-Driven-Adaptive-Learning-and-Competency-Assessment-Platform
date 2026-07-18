using EduTwin.Contracts.Common;

namespace EduTwin.BLL.Organization;

public class GetCenterProfileResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }

    public string? CenterId { get; set; }
    public string? CenterCode { get; set; }
    public string? CenterName { get; set; }
    public string? Status { get; set; }
    public string? Timezone { get; set; }
    public string? RowVersion { get; set; }

    public static GetCenterProfileResult Success(string centerId, string centerCode, string centerName, string status, string timezone, string rowVersion)
    {
        return new GetCenterProfileResult
        {
            IsSuccess = true,
            CenterId = centerId,
            CenterCode = centerCode,
            CenterName = centerName,
            Status = status,
            Timezone = timezone,
            RowVersion = rowVersion
        };
    }

    public static GetCenterProfileResult Failure(string errorCode)
    {
        return new GetCenterProfileResult
        {
            IsSuccess = false,
            ErrorCode = errorCode
        };
    }
}
