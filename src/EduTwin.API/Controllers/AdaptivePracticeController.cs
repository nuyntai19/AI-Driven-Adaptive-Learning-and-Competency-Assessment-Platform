using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.API.Security;
using EduTwin.BLL.AdaptivePractice;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AdaptivePractice;
using EduTwin.Contracts.Common;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/adaptive-practice")]
[Authorize]
public sealed class AdaptivePracticeController : ControllerBase
{
    private readonly IAdaptivePracticeEngine _engine;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public AdaptivePracticeController(
        IAdaptivePracticeEngine engine,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    [HttpGet("proposal")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(AdaptivePracticeProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProposal(
        [FromQuery] Guid subjectId,
        [FromQuery] ulong? customTopicId = null,
        [FromQuery] AdaptivePracticeMode mode = AdaptivePracticeMode.Weakness,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        if (subjectId == Guid.Empty)
        {
            return BadRequest(CreateProblem("INVALID_SUBJECT", "Mã môn học không hợp lệ."));
        }

        try
        {
            var proposal = await _engine.GetProposalAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, subjectId, customTopicId, mode, ct);
            return Ok(proposal);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CreateProblem("PROPOSAL_FAILED", ex.Message));
        }
    }

    [HttpGet("active-session")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(AdaptivePracticeSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSession(
        [FromQuery] Guid? subjectId = null,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var session = await _engine.GetActiveSessionAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, subjectId, ct);
        return Ok(session);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(AdaptivePracticeSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionById(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var session = await _engine.GetSessionByIdAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionId, ct);
        if (session == null)
        {
            return NotFound(CreateProblem("SESSION_NOT_FOUND", "Phiên luyện tập không tồn tại."));
        }

        return Ok(session);
    }

    [HttpPost("sessions")]
    [Authorize(Policy = "recommendations.student.update_own")]
    [ProducesResponseType(typeof(AdaptivePracticeSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateAdaptivePracticeSessionRequest request,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        if (request.SubjectId == Guid.Empty || request.TopicNodeId == 0)
        {
            return BadRequest(CreateProblem("VALIDATION_FAILED", "Môn học và chủ đề luyện tập là bắt buộc."));
        }

        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var session = await _engine.CreateSessionAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, request, now, ct);
            return StatusCode(StatusCodes.Status201Created, session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CreateProblem("CREATE_SESSION_FAILED", ex.Message));
        }
    }

    [HttpPost("sessions/{sessionId:guid}/resume")]
    [Authorize(Policy = "recommendations.student.update_own")]
    [ProducesResponseType(typeof(AdaptivePracticeSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResumeSession(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var session = await _engine.ResumeSessionAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionId, now, ct);
        return Ok(session);
    }

    [HttpPost("sessions/{sessionId:guid}/pause")]
    [Authorize(Policy = "recommendations.student.update_own")]
    [ProducesResponseType(typeof(AdaptivePracticeSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PauseSession(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var session = await _engine.PauseSessionAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionId, now, ct);
        return Ok(session);
    }

    [HttpPost("sessions/{sessionId:guid}/close")]
    [Authorize(Policy = "recommendations.student.update_own")]
    [ProducesResponseType(typeof(AdaptivePracticeSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseSession(
        [FromRoute] Guid sessionId,
        [FromQuery] AdaptivePracticeCloseReason closeReason = AdaptivePracticeCloseReason.StudentStopped,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var summary = await _engine.CloseSessionAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionId, closeReason, now, ct);
        return Ok(summary);
    }

    [HttpGet("sessions/{sessionId:guid}/next-question")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(AdaptivePracticeQuestionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNextQuestion(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var question = await _engine.GetNextQuestionAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionId, now, ct);
        return Ok(question);
    }

    [HttpGet("sessions/{sessionId:guid}/hint")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(AdaptivePracticeHintDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHint(
        [FromRoute] Guid sessionId,
        [FromQuery] ulong sessionItemId,
        [FromQuery] int level = 1,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        try
        {
            var hint = await _engine.GetHintAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionId, sessionItemId, level, ct);
            return Ok(hint);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CreateProblem("HINT_ERROR", ex.Message));
        }
    }

    [HttpPost("sessions/{sessionId:guid}/submit")]
    [Authorize(Policy = "recommendations.student.update_own")]
    [ProducesResponseType(typeof(AdaptivePracticeAnswerFeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] Guid sessionId,
        [FromBody] SubmitAdaptivePracticeAnswerRequest request,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        if (request.ClientSubmissionId == Guid.Empty || request.QuestionId == 0 || request.SessionItemId == 0)
        {
            return BadRequest(CreateProblem("VALIDATION_FAILED", "Dữ liệu nộp câu trả lời không đầy đủ."));
        }

        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var feedback = await _engine.SubmitAnswerAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionId, request, now, ct);
            return Ok(feedback);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CreateProblem("SUBMISSION_FAILED", ex.Message));
        }
    }

    [HttpGet("sessions/{sessionId:guid}/summary")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(AdaptivePracticeSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var summary = await _engine.GetSummaryAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, sessionId, ct);
        if (summary == null)
        {
            return NotFound(CreateProblem("SUMMARY_NOT_FOUND", "Tổng kết chưa sẵn sàng hoặc phiên chưa hoàn tất."));
        }

        return Ok(summary);
    }

    [HttpGet("history")]
    [Authorize(Policy = "recommendations.student.read_own")]
    [ProducesResponseType(typeof(AdaptivePracticeHistoryItemDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] Guid? subjectId = null,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var history = await _engine.GetHistoryAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, subjectId, Math.Clamp(limit, 1, 50), ct);
        return Ok(history);
    }

    [HttpGet("teacher/sessions")]
    [Authorize(Policy = CompositePermissionPolicies.DashboardsClassRead)]
    [ProducesResponseType(typeof(TeacherAdaptivePracticeSessionDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherSessions(
        [FromQuery] Guid? classId = null,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var sessions = await _engine.GetTeacherSessionsAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, classId, subjectId, Math.Clamp(limit, 1, 100), ct);
        return Ok(sessions);
    }

    [HttpGet("teacher/alerts")]
    [Authorize(Policy = CompositePermissionPolicies.DashboardsClassRead)]
    [ProducesResponseType(typeof(TeacherAdaptivePracticeAlertDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherAlerts(
        [FromQuery] Guid? classId = null,
        CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return Forbid();
        }

        var alerts = await _engine.GetTeacherAlertsAsync(_tenantContext.CenterId.Value, _tenantContext.UserId.Value, classId, ct);
        return Ok(alerts);
    }

    private ProblemDetails CreateProblem(string errorCode, string detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var p = new ProblemDetails
        {
            Title = "Yêu cầu không hợp lệ",
            Detail = detail,
            Status = StatusCodes.Status400BadRequest,
            Instance = HttpContext.Request.Path
        };
        p.Extensions["traceId"] = traceId;
        p.Extensions["errorCode"] = errorCode;
        return p;
    }
}
