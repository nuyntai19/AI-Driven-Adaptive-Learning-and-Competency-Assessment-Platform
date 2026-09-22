using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.API.Security;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations.UseCases;
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
        IUpdateLearningPathSessionUseCase? updateLearningPathSessionUseCase = null)
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
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var topics = await _getTopicsUseCase.ExecuteAsync(subjectId, cancellationToken);
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
        var path = await _generateLearningPathUseCase.ExecuteAsync(request, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var path = await _getDetailedLearningPathUseCase.ExecuteAsync(subjectId, cancellationToken);
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
        var updated = await _updateLearningPathSessionUseCase.ExecuteAsync(subjectId, sessionId, request, cancellationToken);
        return updated ? NoContent() : BadRequest(CreateProblemDetails(400, "https://edutwin.local/problems/validation", "Không thể cập nhật buổi học", "Buổi học hoặc trạng thái không hợp lệ.", Activity.Current?.Id ?? HttpContext.TraceIdentifier, ErrorCodes.ValidationFailed));
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
