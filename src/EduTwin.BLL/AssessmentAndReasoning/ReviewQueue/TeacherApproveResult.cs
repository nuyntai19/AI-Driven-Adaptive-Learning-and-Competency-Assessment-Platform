using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public enum TeacherApproveStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Forbidden,
    Conflict
}

public sealed class TeacherApproveResult
{
    public TeacherApproveStatus Status { get; private init; }
    public TeacherApproveDataDto? Data { get; private init; }
    public string ErrorCode { get; private init; } = string.Empty;
    public string ErrorMessage { get; private init; } = string.Empty;

    public static TeacherApproveResult Success(TeacherApproveDataDto data) => new()
    {
        Status = TeacherApproveStatus.Success,
        Data = data
    };

    public static TeacherApproveResult ValidationFailed(string errorCode = "VALIDATION_FAILED", string message = "Dữ liệu yêu cầu không hợp lệ.") => new()
    {
        Status = TeacherApproveStatus.ValidationFailed,
        ErrorCode = errorCode,
        ErrorMessage = message
    };

    public static TeacherApproveResult NotFound(string errorCode = "ANALYSIS_NOT_FOUND", string message = "Không tìm thấy phân tích reasoning tương ứng.") => new()
    {
        Status = TeacherApproveStatus.NotFound,
        ErrorCode = errorCode,
        ErrorMessage = message
    };

    public static TeacherApproveResult Forbidden(string errorCode = "FORBIDDEN", string message = "Bạn không có quyền can thiệp vào bài nộp của học sinh này.") => new()
    {
        Status = TeacherApproveStatus.Forbidden,
        ErrorCode = errorCode,
        ErrorMessage = message
    };

    public static TeacherApproveResult Conflict(string errorCode = "CONCURRENCY_CONFLICT", string message = "Phiên bản dữ liệu đã bị thay đổi bởi người dùng khác.") => new()
    {
        Status = TeacherApproveStatus.Conflict,
        ErrorCode = errorCode,
        ErrorMessage = message
    };
}
