using System.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;
using EduTwin.BLL.AssessmentAndReasoning.Polling;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.AssessmentAndReasoning.Retry;
using EduTwin.BLL.AssessmentAndReasoning.ReviewRequest;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.API.Security;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using EduTwin.BLL.Recommendations.UseCases;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/learning")]
[Authorize]
public sealed class LearningController : ControllerBase
{
    private readonly ISubmitAttemptUseCase _submitAttemptUseCase;
    private readonly IListAttemptsUseCase _listAttemptsUseCase;
    private readonly IGetAnalysisJobStatusUseCase _getAnalysisJobStatusUseCase;
    private readonly IGetNextQuestionUseCase _getNextQuestionUseCase;
    private readonly IGetAttemptFeedbackUseCase _getAttemptFeedbackUseCase;
    private readonly IRetryAttemptAIAnalysisUseCase? _retryAttemptAIAnalysisUseCase;
    private readonly ICreateStudentReviewRequestUseCase? _createStudentReviewRequestUseCase;
    private readonly ILearningSessionExecutionService? _sessionExecutionService;
    private readonly ITenantContext? _tenantContext;
    private readonly TimeProvider _timeProvider;

    public LearningController(
        ISubmitAttemptUseCase submitAttemptUseCase,
        IListAttemptsUseCase listAttemptsUseCase,
        IGetAnalysisJobStatusUseCase getAnalysisJobStatusUseCase,
        TimeProvider timeProvider)
        : this(submitAttemptUseCase, listAttemptsUseCase, getAnalysisJobStatusUseCase, null!, null!, timeProvider, null, null)
    {
    }

    public LearningController(
        ISubmitAttemptUseCase submitAttemptUseCase,
        IListAttemptsUseCase listAttemptsUseCase,
        IGetAnalysisJobStatusUseCase getAnalysisJobStatusUseCase,
        IGetNextQuestionUseCase getNextQuestionUseCase,
        TimeProvider timeProvider)
        : this(submitAttemptUseCase, listAttemptsUseCase, getAnalysisJobStatusUseCase, getNextQuestionUseCase, null!, timeProvider, null, null)
    {
    }

    [ActivatorUtilitiesConstructor]
    public LearningController(
        ISubmitAttemptUseCase submitAttemptUseCase,
        IListAttemptsUseCase listAttemptsUseCase,
        IGetAnalysisJobStatusUseCase getAnalysisJobStatusUseCase,
        IGetNextQuestionUseCase getNextQuestionUseCase,
        IGetAttemptFeedbackUseCase getAttemptFeedbackUseCase,
        TimeProvider timeProvider,
        IRetryAttemptAIAnalysisUseCase? retryAttemptAIAnalysisUseCase = null,
        ICreateStudentReviewRequestUseCase? createStudentReviewRequestUseCase = null,
        ILearningSessionExecutionService? sessionExecutionService = null,
        ITenantContext? tenantContext = null)
    {
        _submitAttemptUseCase = submitAttemptUseCase;
        _listAttemptsUseCase = listAttemptsUseCase;
        _getAnalysisJobStatusUseCase = getAnalysisJobStatusUseCase;
        _getNextQuestionUseCase = getNextQuestionUseCase;
        _getAttemptFeedbackUseCase = getAttemptFeedbackUseCase;
        _retryAttemptAIAnalysisUseCase = retryAttemptAIAnalysisUseCase;
        _createStudentReviewRequestUseCase = createStudentReviewRequestUseCase;
        _sessionExecutionService = sessionExecutionService;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    [HttpGet("next-question")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(NextQuestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNextQuestion(
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? classId = null, [FromQuery] bool history = false, [FromQuery] string? sessionId = null)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (subjectId == Guid.Empty)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                "Mã môn học (subjectId) không hợp lệ.",
                traceId,
                "INVALID_SUBJECT_ID"));
        }

        var result = !string.IsNullOrWhiteSpace(sessionId)
            ? await _getNextQuestionUseCase.ExecuteSessionAsync(subjectId, sessionId, classId, history, cancellationToken)
            : classId.HasValue || history
            ? await _getNextQuestionUseCase.ExecuteAsync(subjectId, classId, history, cancellationToken)
            : await _getNextQuestionUseCase.ExecuteAsync(subjectId, cancellationToken);

        if (result.Forbidden)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    result.ErrorMessage ?? "Bạn không có quyền truy cập.",
                    traceId,
                    ErrorCodes.ForbiddenResource));
        }

        if (result.NotFound || result.Data is null)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage ?? "Không tìm thấy câu hỏi hoặc gợi ý học tập tiếp theo.",
                traceId,
                ErrorCodes.ResourceNotFound));
        }

        return Ok(new NextQuestionResponse
        {
            Data = result.Data,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpGet("attempts")]
    [Authorize(Policy = CompositePermissionPolicies.AttemptsRead)]
    [ProducesResponseType(typeof(AttemptListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAttempts(
        [FromQuery] ListAttemptsQuery query,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _listAttemptsUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new AttemptListResponse
            {
                Data = result.Data ?? throw new InvalidOperationException(
                    "Attempt list succeeded without response data."),
                Meta = new PagedMetaDto
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages,
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return MapAttemptListErrorToResponse(result.ErrorCode, traceId);
    }

    [HttpPost("attempts")]
    [Authorize(Policy = AuthorizationPolicies.StudentOnly)]
    [Authorize(Policy = "learning.attempts.submit")]
    [ProducesResponseType(typeof(SubmitAttemptResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitAttempt(
        [FromBody] SubmitAttemptRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _submitAttemptUseCase.ExecuteAsync(
            request,
            correlationId,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Accepted(new SubmitAttemptResponse
            {
                Data = result.Data ??
                    throw new InvalidOperationException("Submit attempt succeeded without response data."),
                Meta = new MetaDto
                {
                    TraceId = correlationId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return MapErrorToResponse(result.ErrorCode, correlationId);
    }

    [HttpGet("analysis-jobs/{analysisJobId}")]
    [Authorize(Policy = CompositePermissionPolicies.AttemptsRead)]
    [ProducesResponseType(typeof(AnalysisJobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysisJobStatus(
        [FromRoute] string analysisJobId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _getAnalysisJobStatusUseCase.ExecuteAsync(
            analysisJobId,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new AnalysisJobStatusResponse
            {
                Data = result.Data ?? throw new InvalidOperationException(
                    "Analysis job status succeeded without response data."),
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return MapAnalysisJobErrorToResponse(result.ErrorCode, traceId);
    }

    [HttpGet("attempts/{attemptId}/feedback")]
    [Authorize(Policy = CompositePermissionPolicies.AttemptsRead)]
    [ProducesResponseType(typeof(AttemptFeedbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttemptFeedback(
        [FromRoute] ulong attemptId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (attemptId == 0)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                "Mã bài làm (attemptId) không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed));
        }

        if (_getAttemptFeedbackUseCase is null)
        {
            throw new InvalidOperationException("GetAttemptFeedbackUseCase is not configured.");
        }

        var result = await _getAttemptFeedbackUseCase.ExecuteAsync(attemptId, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new AttemptFeedbackResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return result.ErrorCode switch
        {
            ErrorCodes.ForbiddenResource => StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    result.ErrorMessage ?? "Bạn không có quyền xem phản hồi bài làm này.",
                    traceId,
                    ErrorCodes.ForbiddenResource)),

            ErrorCodes.ResourceNotFound => NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage ?? "Không tìm thấy bài làm hoặc kết quả phân tích.",
                traceId,
                ErrorCodes.ResourceNotFound)),

            _ => BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                result.ErrorMessage ?? "Yêu cầu không hợp lệ.",
                traceId,
                result.ErrorCode ?? ErrorCodes.ValidationFailed))
        };
    }

    [HttpPost("attempts/{attemptId}/retry-ai")]
    [Authorize(Policy = "learning.attempts.submit")]
    [ProducesResponseType(typeof(RetryAIAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryAIAnalysis(
        [FromRoute] ulong attemptId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (attemptId == 0)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                "Mã bài làm (attemptId) không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed));
        }

        if (_retryAttemptAIAnalysisUseCase is null)
        {
            throw new InvalidOperationException("RetryAttemptAIAnalysisUseCase is not configured.");
        }

        var result = await _retryAttemptAIAnalysisUseCase.ExecuteAsync(attemptId, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND")
            {
                return NotFound(CreateProblemDetails(
                    StatusCodes.Status404NotFound,
                    "https://edutwin.local/problems/not-found",
                    "Không tìm thấy dữ liệu",
                    result.ErrorMessage ?? "Không tìm thấy bài làm.",
                    traceId,
                    result.ErrorCode));
            }
            if (result.ErrorCode == "FORBIDDEN")
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblemDetails(
                        StatusCodes.Status403Forbidden,
                        "https://edutwin.local/problems/forbidden",
                        "Không có quyền truy cập",
                        result.ErrorMessage ?? "Bạn không có quyền thực hiện chấm lại bài làm này.",
                        traceId,
                        result.ErrorCode));
            }
            if (result.ErrorCode == "JOB_PROCESSING")
            {
                return StatusCode(
                    StatusCodes.Status409Conflict,
                    CreateProblemDetails(
                        StatusCodes.Status409Conflict,
                        "https://edutwin.local/problems/conflict",
                        "AI đang xử lý",
                        result.ErrorMessage ?? "AI đang phân tích bài làm, vui lòng chờ hoàn tất.",
                        traceId,
                        result.ErrorCode));
            }
            if (result.ErrorCode == "JOB_ALREADY_COMPLETED")
            {
                return BadRequest(CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "https://edutwin.local/problems/bad-request",
                    "Đã có kết quả",
                    result.ErrorMessage ?? "Bài làm đã hoàn thành phân tích AI.",
                    traceId,
                    result.ErrorCode));
            }
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Không thể chấm lại",
                result.ErrorMessage ?? "Yêu cầu chấm lại không hợp lệ.",
                traceId,
                result.ErrorCode ?? ErrorCodes.ValidationFailed));
        }

        return Ok(new RetryAIAnalysisResponse
        {
            Data = result.Data!,
            Meta = new MetaDto
            {
                TraceId = traceId,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    [HttpPost("attempts/{attemptId}/review-request")]
    [Authorize(Policy = "learning.attempts.submit")]
    [ProducesResponseType(typeof(StudentReviewRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReviewRequest(
        [FromRoute] ulong attemptId,
        [FromBody] CreateStudentReviewRequest request,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (attemptId == 0)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                "Mã bài làm (attemptId) không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed));
        }

        if (_createStudentReviewRequestUseCase is null)
        {
            throw new InvalidOperationException("CreateStudentReviewRequestUseCase is not configured.");
        }

        var result = await _createStudentReviewRequestUseCase.ExecuteAsync(attemptId, request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "NOT_FOUND")
            {
                return NotFound(CreateProblemDetails(
                    StatusCodes.Status404NotFound,
                    "https://edutwin.local/problems/not-found",
                    "Không tìm thấy dữ liệu",
                    result.ErrorMessage ?? "Không tìm thấy bài làm.",
                    traceId,
                    result.ErrorCode));
            }
            if (result.ErrorCode == "FORBIDDEN")
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblemDetails(
                        StatusCodes.Status403Forbidden,
                        "https://edutwin.local/problems/forbidden",
                        "Không có quyền truy cập",
                        result.ErrorMessage ?? "Bạn không có quyền gửi yêu cầu xem xét cho bài làm này.",
                        traceId,
                        result.ErrorCode));
            }
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                result.ErrorMessage ?? "Yêu cầu xem xét không hợp lệ.",
                traceId,
                result.ErrorCode ?? ErrorCodes.ValidationFailed));
        }

        return Ok(new StudentReviewRequestResponse
        {
            Data = result.Data!,
            Meta = new MetaDto
            {
                TraceId = traceId,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    private IActionResult MapErrorToResponse(string? errorCode, string traceId) =>
        errorCode switch
        {
            ErrorCodes.ValidationFailed => BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/validation",
                "Dữ liệu không hợp lệ",
                "Một hoặc nhiều trường trong bài làm không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed)),

            ErrorCodes.ResourceNotFound => NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                "Không tìm thấy học sinh hoặc câu hỏi trong phạm vi trung tâm hiện tại.",
                traceId,
                ErrorCodes.ResourceNotFound)),

            ErrorCodes.DuplicateSubmission => Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "https://edutwin.local/problems/duplicate-submission",
                "Bài làm bị trùng",
                "Mã gửi bài đã được dùng cho một nội dung khác.",
                traceId,
                ErrorCodes.DuplicateSubmission)),

            ErrorCodes.AssignmentNotAvailable => UnprocessableEntity(CreateProblemDetails(
                StatusCodes.Status422UnprocessableEntity,
                "https://edutwin.local/problems/assignment-not-available",
                "Bài tập không khả dụng",
                "Bài tập không còn khả dụng hoặc học sinh không thuộc danh sách được giao.",
                traceId,
                ErrorCodes.AssignmentNotAvailable)),

            ErrorCodes.QuestionReasoningRequired => UnprocessableEntity(CreateProblemDetails(
                StatusCodes.Status422UnprocessableEntity,
                "https://edutwin.local/problems/reasoning-required",
                "Thiếu phần giải thích",
                "Câu hỏi này yêu cầu học sinh nhập phần giải thích trước khi gửi.",
                traceId,
                ErrorCodes.QuestionReasoningRequired)),

            ErrorCodes.UploadTokenAlreadyUsed => Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "https://edutwin.local/problems/upload-token-already-used",
                "Mã tải lên đã được sử dụng",
                "Mã tải lên đính kèm đã được sử dụng cho một bài nộp khác.",
                traceId,
                ErrorCodes.UploadTokenAlreadyUsed)),

            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };

    private IActionResult MapAnalysisJobErrorToResponse(
        string? errorCode,
        string traceId) =>
        errorCode switch
        {
            ErrorCodes.ValidationFailed => BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/validation",
                "Dữ liệu không hợp lệ",
                "Mã công việc phân tích không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed)),

            ErrorCodes.ForbiddenResource => StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    "Bạn không có quyền xem công việc phân tích này.",
                    traceId,
                    ErrorCodes.ForbiddenResource)),

            ErrorCodes.ResourceNotFound => NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                "Không tìm thấy công việc phân tích trong phạm vi trung tâm hiện tại.",
                traceId,
                ErrorCodes.ResourceNotFound)),

            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };

    private IActionResult MapAttemptListErrorToResponse(
        string? errorCode,
        string traceId) =>
        errorCode switch
        {
            ErrorCodes.ValidationFailed => BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/validation",
                "Dữ liệu không hợp lệ",
                "Một hoặc nhiều bộ lọc danh sách bài làm không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed)),

            ErrorCodes.ForbiddenResource => StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    "Bạn không có quyền xem danh sách bài làm của học sinh này.",
                    traceId,
                    ErrorCodes.ForbiddenResource)),

            ErrorCodes.ResourceNotFound => NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                "Không tìm thấy học sinh hoặc bộ lọc trong phạm vi trung tâm hiện tại.",
                traceId,
                ErrorCodes.ResourceNotFound)),

            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };

    [HttpPost("sessions/{sessionId}/start")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(StartSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartSession(
        [FromRoute] string sessionId,
        [FromBody] StartSessionRequest? body,
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? classId,
        [FromQuery] bool history = false,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_sessionExecutionService is null || _tenantContext is null)
        {
            throw new InvalidOperationException("Session execution service is not configured.");
        }

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(
                StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden",
                "Không có quyền truy cập", "Tenant context is not resolved.", traceId, ErrorCodes.ForbiddenResource));
        }

        var effectiveSubjectId = body?.SubjectId != null && body.SubjectId != Guid.Empty
            ? body.SubjectId
            : (subjectId ?? Guid.Empty);

        if (effectiveSubjectId == Guid.Empty)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest, "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ", "Mã môn học (subjectId) không hợp lệ.", traceId, "INVALID_SUBJECT_ID"));
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;
        var effectiveClassId = body?.ClassId ?? classId;

        try
        {
            var result = await _sessionExecutionService.StartOrResumeSessionAsync(
                centerId, studentId, effectiveSubjectId, sessionId, effectiveClassId, cancellationToken);

            NextQuestionDto? questionDto = null;
            if (result.CurrentQuestionId.HasValue)
            {
                bool isCheckpoint = result.CheckpointQuestionId.HasValue && result.CurrentQuestionId.Value == result.CheckpointQuestionId.Value;
                questionDto = await _sessionExecutionService.GetQuestionDtoAsync(
                    centerId, result.SessionId, result.CurrentQuestionId.Value, isCheckpoint, cancellationToken);
                if (questionDto is not null)
                {
                    questionDto.SessionRunId = result.SessionRunId;
                    questionDto.SessionId = result.SessionId;
                    questionDto.QuestionIndex = 1;
                }
            }

            return Ok(new StartSessionResponse
            {
                Data = new StartSessionDto
                {
                    SessionRunId = result.SessionRunId,
                    SessionId = result.SessionId,
                    RunNumber = result.RunNumber,
                    Status = result.Status,
                    EvaluationStatus = result.EvaluationStatus,
                    CurrentQuestionId = result.CurrentQuestionId,
                    CheckpointQuestionId = result.CheckpointQuestionId,
                    IsResumed = result.IsResumed,
                    CurrentQuestion = questionDto
                },
                Meta = new MetaDto
                {
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                    TraceId = traceId
                }
            });
        }
        catch (QuestionPoolDepletedException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, CreateProblemDetails(
                StatusCodes.Status409Conflict, "https://edutwin.local/problems/conflict",
                "Ngân hàng câu hỏi không đủ", ex.Message, traceId, "QUESTION_POOL_DEPLETED"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound, "https://edutwin.local/problems/not-found",
                "Không tìm thấy buổi học", ex.Message, traceId, ErrorCodes.ResourceNotFound));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest, "https://edutwin.local/problems/validation",
                "Không thể bắt đầu buổi học", ex.Message, traceId, ErrorCodes.ValidationFailed));
        }
    }

    [HttpGet("sessions/{sessionId}/resume")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(ResumeSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeSession(
        [FromRoute] string sessionId,
        [FromQuery] Guid subjectId,
        [FromQuery] Guid? classId,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_sessionExecutionService is null || _tenantContext is null)
        {
            throw new InvalidOperationException("Session execution service is not configured.");
        }

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(
                StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden",
                "Không có quyền truy cập", "Tenant context is not resolved.", traceId, ErrorCodes.ForbiddenResource));
        }

        if (subjectId == Guid.Empty)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest, "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ", "Mã môn học (subjectId) không hợp lệ.", traceId, "INVALID_SUBJECT_ID"));
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        try
        {
            var resumeResult = await _sessionExecutionService.ResumeSessionAsync(
                centerId, studentId, subjectId, sessionId, classId, cancellationToken);

            NextQuestionDto? questionDto = null;
            if (resumeResult.CurrentQuestionId.HasValue)
            {
                questionDto = await _sessionExecutionService.GetQuestionDtoAsync(
                    centerId, resumeResult.SessionId, resumeResult.CurrentQuestionId.Value, false, cancellationToken);
                if (questionDto is not null && resumeResult.SessionRunId.HasValue)
                {
                    questionDto.SessionRunId = resumeResult.SessionRunId.Value;
                    questionDto.SessionId = resumeResult.SessionId;
                    questionDto.TotalQuestions = resumeResult.TotalQuestions;
                    questionDto.QuestionIndex = resumeResult.QuestionsAttempted + 1;
                }
            }

            return Ok(new ResumeSessionResponse
            {
                Data = new ResumeSessionDto
                {
                    SessionRunId = resumeResult.SessionRunId,
                    SessionId = resumeResult.SessionId,
                    LegacySessionId = resumeResult.LegacySessionId,
                    SessionTitle = resumeResult.SessionTitle,
                    SessionStatus = resumeResult.SessionStatus,
                    RunNumber = resumeResult.RunNumber,
                    RunStatus = resumeResult.RunStatus,
                    EvaluationStatus = resumeResult.EvaluationStatus,
                    Outcome = resumeResult.Outcome,
                    StartedAt = resumeResult.StartedAt,
                    LastActivityAt = resumeResult.LastActivityAt,
                    CompletedAt = resumeResult.CompletedAt,
                    TotalQuestions = resumeResult.TotalQuestions,
                    QuestionsAttempted = resumeResult.QuestionsAttempted,
                    QuestionsCorrect = resumeResult.QuestionsCorrect,
                    ScoreRate = resumeResult.ScoreRate,
                    CurrentQuestionId = resumeResult.CurrentQuestionId,
                    CurrentQuestion = questionDto,
                    AttemptedQuestionIds = resumeResult.AttemptedQuestionIds,
                    IsCompleted = resumeResult.IsCompleted,
                    IsGrading = resumeResult.IsGrading,
                    CanStart = resumeResult.CanStart
                },
                Meta = new MetaDto
                {
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                    TraceId = traceId
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound, "https://edutwin.local/problems/not-found",
                "Không tìm thấy buổi học", ex.Message, traceId, ErrorCodes.ResourceNotFound));
        }
    }

    [HttpPost("sessions/{sessionId}/evaluate")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(EvaluateSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EvaluateSession(
        [FromRoute] string sessionId,
        [FromBody] EvaluateSessionRequest? body,
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? classId,
        [FromQuery] ulong? sessionRunId,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_sessionExecutionService is null || _tenantContext is null)
        {
            throw new InvalidOperationException("Session execution service is not configured.");
        }

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(
                StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden",
                "Không có quyền truy cập", "Tenant context is not resolved.", traceId, ErrorCodes.ForbiddenResource));
        }

        var effectiveSubjectId = body?.SubjectId != null && body.SubjectId != Guid.Empty
            ? body.SubjectId.Value
            : (subjectId ?? Guid.Empty);

        if (effectiveSubjectId == Guid.Empty)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest, "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ", "Mã môn học (subjectId) không hợp lệ.", traceId, "INVALID_SUBJECT_ID"));
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;
        var effectiveClassId = body?.ClassId ?? classId;
        var effectiveRunId = body?.SessionRunId ?? sessionRunId;

        try
        {
            var result = await _sessionExecutionService.EvaluateSessionAsync(
                centerId, studentId, effectiveSubjectId, sessionId, effectiveClassId, effectiveRunId, cancellationToken);

            return Ok(new EvaluateSessionResponse
            {
                Data = new EvaluateSessionDto
                {
                    SessionRunId = result.SessionRunId,
                    SessionId = result.SessionId,
                    LegacySessionId = result.LegacySessionId,
                    SessionTitle = result.SessionTitle,
                    SessionStatus = result.SessionStatus,
                    RunNumber = result.RunNumber,
                    RunStatus = result.RunStatus,
                    EvaluationStatus = result.EvaluationStatus,
                    Outcome = result.Outcome,
                    TotalQuestions = result.TotalQuestions,
                    QuestionsAttempted = result.QuestionsAttempted,
                    CheckpointsAttempted = result.CheckpointsAttempted,
                    CheckpointsCorrect = result.CheckpointsCorrect,
                    CheckpointScoreRate = result.CheckpointScoreRate,
                    OverallScoreRate = result.OverallScoreRate,
                    RequiresTeacherReview = result.RequiresTeacherReview,
                    HasPendingAIGrading = result.HasPendingAIGrading,
                    EvaluationDetail = result.EvaluationDetail,
                    EvaluatedAt = result.EvaluatedAt,
                    IsCompleted = result.IsCompleted,
                    Passed = result.Outcome == SessionRunOutcome.Passed
                },
                Meta = new MetaDto
                {
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                    TraceId = traceId
                }
            });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict, "https://edutwin.local/problems/concurrency",
                "Xung đột phiên bản", "Phiên học đã được cập nhật bởi một yêu cầu khác. Vui lòng thử lại.", traceId, ErrorCodes.ConcurrencyConflict));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound, "https://edutwin.local/problems/not-found",
                "Không tìm thấy buổi học", ex.Message, traceId, ErrorCodes.ResourceNotFound));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest, "https://edutwin.local/problems/validation",
                "Không thể đánh giá buổi học", ex.Message, traceId, ErrorCodes.ValidationFailed));
        }
    }

    private ProblemDetails CreateProblemDetails(
        int status,
        string type,
        string title,
        string detail,
        string traceId,
        string errorCode) =>
        new()
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = traceId,
                ["errorCode"] = errorCode
            }
        };
}
