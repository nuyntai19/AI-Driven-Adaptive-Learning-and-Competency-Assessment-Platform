namespace EduTwin.BLL.Organization;

public class UpdateCenterProfileResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public string? CenterId { get; set; }
    public string? CenterCode { get; set; }
    public string? CenterName { get; set; }
    public string? Status { get; set; }
    public string? Timezone { get; set; }
    public string? RowVersion { get; set; }

    public static UpdateCenterProfileResult Success(string centerId, string centerCode, string centerName, string status, string timezone, string rowVersion)
    {
        return new UpdateCenterProfileResult
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

    public static UpdateCenterProfileResult Failure(string errorCode)
    {
        return new UpdateCenterProfileResult
        {
            IsSuccess = false,
            ErrorCode = errorCode
        };
    }
}
