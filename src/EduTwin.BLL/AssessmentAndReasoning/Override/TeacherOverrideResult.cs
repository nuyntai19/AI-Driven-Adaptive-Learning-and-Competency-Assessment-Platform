using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Override;

public enum TeacherOverrideStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Forbidden,
    Conflict
}

public sealed class TeacherOverrideResult
{
    public TeacherOverrideStatus Status { get; private init; }
    public TeacherOverrideDataDto? Data { get; private init; }
    public string ErrorCode { get; private init; } = string.Empty;
    public string ErrorMessage { get; private init; } = string.Empty;

    public static TeacherOverrideResult Success(TeacherOverrideDataDto data) => new()
    {
        Status = TeacherOverrideStatus.Success,
        Data = data
    };

    public static TeacherOverrideResult ValidationFailed(string errorCode = "VALIDATION_FAILED", string message = "Dữ liệu yêu cầu không hợp lệ.") => new()
    {
        Status = TeacherOverrideStatus.ValidationFailed,
        ErrorCode = errorCode,
        ErrorMessage = message
    };

    public static TeacherOverrideResult NotFound(string errorCode = "ANALYSIS_NOT_FOUND", string message = "Không tìm thấy phân tích reasoning tương ứng.") => new()
    {
        Status = TeacherOverrideStatus.NotFound,
        ErrorCode = errorCode,
        ErrorMessage = message
    };

    public static TeacherOverrideResult Forbidden(string errorCode = "FORBIDDEN", string message = "Bạn không có quyền can thiệp vào bài nộp của học sinh này.") => new()
    {
        Status = TeacherOverrideStatus.Forbidden,
        ErrorCode = errorCode,
        ErrorMessage = message
    };

    public static TeacherOverrideResult Conflict(string errorCode = "CONCURRENCY_CONFLICT", string message = "Phiên bản override đã bị thay đổi bởi người dùng khác.") => new()
    {
        Status = TeacherOverrideStatus.Conflict,
        ErrorCode = errorCode,
        ErrorMessage = message
    };
}
