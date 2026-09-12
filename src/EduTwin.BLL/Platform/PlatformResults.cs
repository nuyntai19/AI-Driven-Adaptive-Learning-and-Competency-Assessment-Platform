namespace EduTwin.BLL.Platform;

public record PlatformResult<T>
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public T? Data { get; init; }

    public static PlatformResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static PlatformResult<T> Failure(string errorCode, string? message = null) => new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = message };
}

public record PlatformResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static PlatformResult Success() => new() { IsSuccess = true };
    public static PlatformResult Failure(string errorCode, string? message = null) => new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = message };
}
