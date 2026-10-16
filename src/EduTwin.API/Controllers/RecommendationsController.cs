using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.API.Security;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations.UseCases;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/students/me")]
[Authorize]
public sealed class RecommendationsController : ControllerBase
{
    private readonly IGetActiveRecommendationUseCase _getActiveRecommendationUseCase;
    private readonly IAcceptRecommendationUseCase _acceptRecommendationUseCase;
    private readonly IDismissRecommendationUseCase _dismissRecommendationUseCase;
    private readonly IGetActiveLearningPathUseCase _getActiveLearningPathUseCase;
    private readonly IGetLearningPathTopicsUseCase _getTopicsUseCase;
    private readonly IGetStudentLearningPathPreferenceUseCase _getPreferenceUseCase;
    private readonly IGenerateLearningPathUseCase _generateLearningPathUseCase;
    private readonly IGetDetailedLearningPathUseCase _getDetailedLearningPathUseCase;
    private readonly IUpdateLearningPathSessionUseCase? _updateLearningPathSessionUseCase;
    private readonly ILearningSessionExecutionService? _sessionExecutionService;
    private readonly EduTwin.BLL.Recommendations.Planning.ILearningPathV4Planner? _v4Planner;
    private readonly ILearningPathV4Evaluator? _v4Evaluator;
    private readonly ILearningActivityExecutionService? _activityExecutionService;
    private readonly ITenantContext? _tenantContext;
    private readonly TimeProvider _timeProvider;

    [ActivatorUtilitiesConstructor]
    public RecommendationsController(
        IGetActiveRecommendationUseCase getActiveRecommendationUseCase,
        IAcceptRecommendationUseCase acceptRecommendationUseCase,
        IDismissRecommendationUseCase dismissRecommendationUseCase,
        IGetActiveLearningPathUseCase getActiveLearningPathUseCase,
        TimeProvider timeProvider,
        IGetLearningPathTopicsUseCase? getTopicsUseCase = null,
        IGetStudentLearningPathPreferenceUseCase? getPreferenceUseCase = null,
        IGenerateLearningPathUseCase? generateLearningPathUseCase = null,
        IGetDetailedLearningPathUseCase? getDetailedLearningPathUseCase = null,
        IUpdateLearningPathSessionUseCase? updateLearningPathSessionUseCase = null,
        ILearningSessionExecutionService? sessionExecutionService = null,
        EduTwin.BLL.Recommendations.Planning.ILearningPathV4Planner? v4Planner = null,
        ILearningPathV4Evaluator? v4Evaluator = null,
        ILearningActivityExecutionService? activityExecutionService = null,
        ITenantContext? tenantContext = null)
    {
        _getActiveRecommendationUseCase = getActiveRecommendationUseCase;
        _acceptRecommendationUseCase = acceptRecommendationUseCase;
        _dismissRecommendationUseCase = dismissRecommendationUseCase;
        _getActiveLearningPathUseCase = getActiveLearningPathUseCase;
        _timeProvider = timeProvider;
        _getTopicsUseCase = getTopicsUseCase!;
        _getPreferenceUseCase = getPreferenceUseCase!;
        _generateLearningPathUseCase = generateLearningPathUseCase!;
        _getDetailedLearningPathUseCase = getDetailedLearningPathUseCase!;
        _updateLearningPathSessionUseCase = updateLearningPathSessionUseCase;
        _sessionExecutionService = sessionExecutionService;
        _v4Planner = v4Planner;
        _v4Evaluator = v4Evaluator;
        _activityExecutionService = activityExecutionService;
        _tenantContext = tenantContext;
    }

    public RecommendationsController(
        IGetActiveRecommendationUseCase getActiveRecommendationUseCase,
        IAcceptRecommendationUseCase acceptRecommendationUseCase,
        IDismissRecommendationUseCase dismissRecommendationUseCase,
        IGetActiveLearningPathUseCase getActiveLearningPathUseCase,
        IGetLearningPathTopicsUseCase? getTopicsUseCase,
        IGetStudentLearningPathPreferenceUseCase? getPreferenceUseCase,
        IGenerateLearningPathUseCase? generateLearningPathUseCase,
        IGetDetailedLearningPathUseCase? getDetailedLearningPathUseCase,
        TimeProvider timeProvider)
        : this(getActiveRecommendationUseCase, acceptRecommendationUseCase, dismissRecommendationUseCase, getActiveLearningPathUseCase, timeProvider, getTopicsUseCase, getPreferenceUseCase, generateLearningPathUseCase, getDetailedLearningPathUseCase)
    {
    }

    [HttpGet("recommendation")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveRecommendation(
        [FromQuery] Guid? subjectId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _getActiveRecommendationUseCase.ExecuteAsync(subjectId, cancellationToken);

        if (result.Forbidden)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    result.ErrorMessage ?? "Bạn không có quyền xem gợi ý học tập này.",
                    traceId,
                    ErrorCodes.ForbiddenResource));
        }

        if (result.NotFound || result.Data is null)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage ?? "Không có đề xuất học tập đang hoạt động.",
                traceId,
                ErrorCodes.ResourceNotFound));
        }

        return Ok(new RecommendationResponse
        {
            Data = result.Data,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpPost("recommendation/{id}/accept")]
    [Authorize(Policy = "recommendations.student.update_own")]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptRecommendation(
        [FromRoute] ulong id,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (id == 0)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                "Mã đề xuất không hợp lệ.",
                traceId,
                "INVALID_RECOMMENDATION_ID"));
        }

        var result = await _acceptRecommendationUseCase.ExecuteAsync(id, cancellationToken);

        if (result.Forbidden)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    result.ErrorMessage ?? "Bạn không có quyền chấp nhận đề xuất này.",
                    traceId,
                    ErrorCodes.ForbiddenResource));
        }

        if (result.NotFound)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage ?? "Đề xuất không tồn tại.",
                traceId,
                ErrorCodes.ResourceNotFound));
        }

        if (result.Conflict)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "https://edutwin.local/problems/conflict",
                "Xung đột trạng thái",
                result.ErrorMessage ?? "Không thể chấp nhận đề xuất ở trạng thái hiện tại.",
                traceId,
                result.ErrorCode ?? "CONFLICT"));
        }

        return Ok(new RecommendationResponse
        {
            Data = result.Data,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpPost("recommendation/{id}/dismiss")]
    [Authorize(Policy = "recommendations.student.update_own")]
    [ProducesResponseType(typeof(RecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DismissRecommendation(
        [FromRoute] ulong id,
        [FromBody] DismissRecommendationRequest? request,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (id == 0)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/bad-request",
                "Dữ liệu không hợp lệ",
                "Mã đề xuất không hợp lệ.",
                traceId,
                "INVALID_RECOMMENDATION_ID"));
        }

        var result = await _dismissRecommendationUseCase.ExecuteAsync(id, request?.Reason, cancellationToken);

        if (result.Forbidden)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    result.ErrorMessage ?? "Bạn không có quyền bỏ qua đề xuất này.",
                    traceId,
                    ErrorCodes.ForbiddenResource));
        }

        if (result.NotFound)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage ?? "Đề xuất không tồn tại.",
                traceId,
                ErrorCodes.ResourceNotFound));
        }

        if (result.Conflict)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "https://edutwin.local/problems/conflict",
                "Xung đột trạng thái",
                result.ErrorMessage ?? "Không thể bỏ qua đề xuất ở trạng thái hiện tại.",
                traceId,
                result.ErrorCode ?? "CONFLICT"));
        }

        return Ok(new RecommendationResponse
        {
            Data = result.Data,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpGet("learning-path")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(LearningPathResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveLearningPath(
        [FromQuery] Guid? subjectId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _getActiveLearningPathUseCase.ExecuteAsync(subjectId, cancellationToken);

        if (result.Forbidden)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    result.ErrorMessage ?? "Bạn không có quyền xem lộ trình học tập này.",
                    traceId,
                    ErrorCodes.ForbiddenResource));
        }

        if (result.NotFound || result.Data is null)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage ?? "Không có lộ trình học tập đang hoạt động.",
                traceId,
                ErrorCodes.ResourceNotFound));
        }

        return Ok(new LearningPathResponse
        {
            Data = result.Data,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpGet("learning-path/topics")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(LearningPathTopicsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLearningPathTopics(
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? classId = null,
        [FromQuery] bool history = false)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var topics = await _getTopicsUseCase.ExecuteScopedAsync(subjectId, classId, history, cancellationToken);
        return Ok(new LearningPathTopicsResponse
        {
            Data = topics,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpGet("learning-path/preferences")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(StudentLearningPathPreferenceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLearningPathPreferences(
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var pref = await _getPreferenceUseCase.ExecuteAsync(subjectId, cancellationToken);
        return Ok(new StudentLearningPathPreferenceResponse
        {
            Data = pref,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpPost("learning-path/generate")]
    [Authorize(Policy = "recommendations.student.update_own")]
    [ProducesResponseType(typeof(DetailedLearningPathResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateLearningPath(
        [FromBody] GenerateLearningPathRequest request,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        DetailedLearningPathDto path;
        try { path = await _generateLearningPathUseCase.ExecuteAsync(request, cancellationToken); }
        catch (EduTwin.BLL.Organization.LearningScopeDeniedException error)
        {
            return Conflict(CreateProblemDetails(409, "https://edutwin.local/problems/invalid-state", "Phạm vi học tập chỉ xem",
                error.Message, traceId, ErrorCodes.InvalidStateTransition));
        }
        catch (ArgumentException error)
        {
            return BadRequest(CreateProblemDetails(400, "https://edutwin.local/problems/validation", "Mục tiêu học tập không hợp lệ", error.Message, traceId, ErrorCodes.ValidationFailed));
        }
        catch (KeyNotFoundException error)
        {
            return NotFound(CreateProblemDetails(404, "https://edutwin.local/problems/not-found", "Không tìm thấy môn học", error.Message, traceId, ErrorCodes.ResourceNotFound));
        }
        catch (LearningPathUnavailableException error)
        {
            return Conflict(CreateProblemDetails(409, "https://edutwin.local/problems/invalid-state", "Chưa thể tạo lộ trình", error.Message, traceId, ErrorCodes.InvalidStateTransition));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Conflict(CreateProblemDetails(409, "https://edutwin.local/problems/concurrency", "Lộ trình đã thay đổi", "Vui lòng tải lại lộ trình rồi thử lại.", traceId, ErrorCodes.ConcurrencyConflict));
        }
        return Ok(new DetailedLearningPathResponse
        {
            Data = path,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpGet("learning-path/detailed")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(DetailedLearningPathResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailedLearningPath(
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken, [FromQuery] Guid? classId = null, [FromQuery] Guid? pathId = null)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var path = pathId.HasValue ? await _getDetailedLearningPathUseCase.ExecuteAsync(subjectId, classId, pathId, cancellationToken)
            : classId.HasValue ? await _getDetailedLearningPathUseCase.ExecuteAsync(subjectId, classId, cancellationToken)
            : await _getDetailedLearningPathUseCase.ExecuteAsync(subjectId, cancellationToken);
        if (path is null)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                "Chưa có lộ trình học tập chi tiết cho môn này. Vui lòng hoàn thành khảo sát để tạo lộ trình.",
                traceId,
                ErrorCodes.ResourceNotFound));
        }

        return Ok(new DetailedLearningPathResponse
        {
            Data = path,
            Meta = new MetaDto
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                TraceId = traceId
            }
        });
    }

    [HttpGet("learning-path/sessions/{sessionId}")]
    [Authorize(Policy = "recommendations.student.read_own")]
    public async Task<IActionResult> GetLearningPathSession(string sessionId, [FromQuery] Guid subjectId,
        CancellationToken cancellationToken, [FromQuery] Guid? classId = null, [FromQuery] bool history = false)
    {
        var session = _updateLearningPathSessionUseCase is null ? null :
            await _updateLearningPathSessionUseCase.GetAsync(subjectId, sessionId, classId, history, cancellationToken);
        return session is null ? NotFound(CreateProblemDetails(404, "https://edutwin.local/problems/not-found", "Không tìm thấy buổi học",
            "Buổi học không tồn tại trong phạm vi đang học.", Activity.Current?.Id ?? HttpContext.TraceIdentifier, ErrorCodes.ResourceNotFound)) :
            Ok(new { data = session });
    }

    [HttpPatch("learning-path/sessions/{sessionId}")]
    [Authorize(Policy = "recommendations.student.update_own")]
    public async Task<IActionResult> UpdateLearningPathSession(
        [FromRoute] string sessionId,
        [FromQuery] Guid subjectId,
        [FromBody] UpdateLearningPathSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (_updateLearningPathSessionUseCase is null)
            throw new InvalidOperationException("UpdateLearningPathSessionUseCase is not configured.");
        bool updated;
        try { updated = await _updateLearningPathSessionUseCase.ExecuteAsync(subjectId, sessionId, request, cancellationToken); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Conflict(CreateProblemDetails(409, "https://edutwin.local/problems/concurrency", "Buổi học đã thay đổi",
                "Vui lòng tải lại buổi học rồi thử lại.", Activity.Current?.Id ?? HttpContext.TraceIdentifier, ErrorCodes.ConcurrencyConflict));
        }
        return updated ? NoContent() : BadRequest(CreateProblemDetails(400, "https://edutwin.local/problems/validation", "Không thể cập nhật buổi học", "Buổi học hoặc trạng thái không hợp lệ.", Activity.Current?.Id ?? HttpContext.TraceIdentifier, ErrorCodes.ValidationFailed));
    }

    [HttpPost("learning-path/sessions/{sessionId}/start")]
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

    [HttpGet("learning-path/sessions/{sessionId}/resume")]
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

    [HttpPost("learning-path/sessions/{sessionId}/evaluate")]
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

    [HttpPost("learning-goals")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(LearningGoalDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrUpdateLearningGoal([FromBody] CreateLearningGoalRequest request, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_v4Planner is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _v4Planner.CreateOrUpdateGoalAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("learning-goals")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(LearningGoalDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveLearningGoal([FromQuery] Guid subjectId, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_v4Planner is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _v4Planner.GetActiveGoalAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, subjectId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("path-proposals")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(PathProposalDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GeneratePathProposal([FromBody] GeneratePathProposalRequest request, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_v4Planner is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _v4Planner.GeneratePathProposalAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("path-proposals/{proposalId:guid}/accept")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(AcceptPathProposalResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptPathProposal([FromRoute] Guid proposalId, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_v4Planner is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _v4Planner.AcceptPathProposalAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, proposalId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("learning-paths/capability-map")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(CapabilityMapDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCapabilityMap([FromQuery] Guid subjectId, [FromQuery] Guid? classId, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_v4Planner is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _v4Planner.GetCapabilityMapAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, subjectId, classId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("learning-agenda")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(LearningAgendaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLearningAgenda([FromQuery] Guid subjectId, [FromQuery] uint availableMinutes = 15, CancellationToken cancellationToken = default, [FromQuery] Guid? classId = null)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_v4Planner is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _v4Planner.GetLearningAgendaAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, subjectId, availableMinutes, cancellationToken, classId);
        return Ok(result);
    }

    [HttpPost("missions/{missionId:guid}/start")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [Authorize(Policy = "learning.attempts.submit")]
    public async Task<IActionResult> StartMission([FromRoute] Guid missionId, CancellationToken cancellationToken)
    {
        if (_activityExecutionService == null || _tenantContext?.IsResolved != true || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return Forbid();
        return Ok(await _activityExecutionService.StartMissionAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, missionId, cancellationToken));
    }

    [HttpGet("activity-runs/{activityRunId:long}")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(ActivityRunDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivityRun([FromRoute] ulong activityRunId, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_activityExecutionService is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _activityExecutionService.GetActivityRunAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, activityRunId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("activity-runs/{activityRunId:long}/draft")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(SaveActivityDraftResponse), StatusCodes.Status200OK)]
    [Authorize(Policy = "learning.attempts.submit")]
    public async Task<IActionResult> SaveActivityDraft([FromRoute] ulong activityRunId, [FromBody] SaveActivityDraftRequest request, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_activityExecutionService is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _activityExecutionService.SaveDraftAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, activityRunId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("activity-runs/{activityRunId:long}/assistance")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(AssistanceResponseDto), StatusCodes.Status200OK)]
    [Authorize(Policy = "learning.attempts.submit")]
    public async Task<IActionResult> RequestActivityAssistance([FromRoute] ulong activityRunId, [FromBody] RequestAssistanceRequest request, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_activityExecutionService is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _activityExecutionService.RequestAssistanceAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, activityRunId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("activity-runs/{activityRunId:long}/submissions")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(ActivitySubmissionResponseDto), StatusCodes.Status200OK)]
    [Authorize(Policy = "learning.attempts.submit")]
    public async Task<IActionResult> SubmitActivity([FromRoute] ulong activityRunId, [FromBody] SubmitActivityRequest request, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_activityExecutionService is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _activityExecutionService.SubmitActivityAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, activityRunId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("missions/{missionId:guid}/runs/{sessionRunId:long}")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    public async Task<IActionResult> GetMissionRun([FromRoute] Guid missionId, [FromRoute] ulong sessionRunId, CancellationToken cancellationToken)
    {
        if (_activityExecutionService == null || _tenantContext?.IsResolved != true || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue) return Forbid();
        return Ok(await _activityExecutionService.GetMissionRunAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, missionId, sessionRunId, cancellationToken));
    }

    [HttpPost("activity-runs/{activityRunId:long}/ai-feedback")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [Authorize(Policy = "learning.attempts.submit")]
    [V4LearningExceptionFilter]
    public async Task<IActionResult> GetActivityAiFeedback([FromRoute] ulong activityRunId, CancellationToken cancellationToken)
    {
        if (_activityExecutionService == null || _tenantContext?.IsResolved != true || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue) return Forbid();
        return Ok(await _activityExecutionService.GetAiFeedbackAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, activityRunId, cancellationToken));
    }

    [HttpPost("mission-runs/{sessionRunId:long}/evaluate-v4")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [V4LearningExceptionFilter]
    [ProducesResponseType(typeof(V4SessionEvaluationResultDto), StatusCodes.Status200OK)]
    [Authorize(Policy = "learning.attempts.submit")]
    public async Task<IActionResult> EvaluateMissionRunV4([FromRoute] ulong sessionRunId, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_v4Evaluator is null || _tenantContext is null || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, CreateProblemDetails(StatusCodes.Status403Forbidden, "https://edutwin.local/problems/forbidden", "Không có quyền", "Tenant not resolved", traceId, ErrorCodes.ForbiddenResource));
        }

        var result = await _v4Evaluator.EvaluateMissionRunAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionRunId, cancellationToken);
        return Ok(result);
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
