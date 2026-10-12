using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/teacher-student-notes")]
[Authorize]
public class TeacherStudentNotesController : ControllerBase
{
    private readonly ICreateTeacherStudentNoteUseCase _createUseCase;
    private readonly IGetTeacherStudentNoteUseCase _getUseCase;
    private readonly IListTeacherStudentNotesUseCase _listUseCase;
    private readonly IUpdateTeacherStudentNoteUseCase _updateUseCase;
    private readonly IDeleteTeacherStudentNoteUseCase _deleteUseCase;
    private readonly IUpsertTeacherStudentNoteUseCase _upsertUseCase;
    private readonly TimeProvider _timeProvider;

    public TeacherStudentNotesController(
        ICreateTeacherStudentNoteUseCase createUseCase,
        IGetTeacherStudentNoteUseCase getUseCase,
        IListTeacherStudentNotesUseCase listUseCase,
        IUpdateTeacherStudentNoteUseCase updateUseCase,
        IDeleteTeacherStudentNoteUseCase deleteUseCase,
        IUpsertTeacherStudentNoteUseCase upsertUseCase,
        TimeProvider timeProvider)
    {
        _createUseCase = createUseCase;
        _getUseCase = getUseCase;
        _listUseCase = listUseCase;
        _updateUseCase = updateUseCase;
        _deleteUseCase = deleteUseCase;
        _upsertUseCase = upsertUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<IActionResult> ListNotes([FromQuery] TeacherStudentNoteListQuery query, CancellationToken cancellationToken)
    {
        var result = await _listUseCase.ExecuteAsync(query, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new TeacherStudentNoteListResponse
            {
                Data = result.Data ?? Array.Empty<TeacherStudentNoteDto>(),
                Meta = new PagedMetaDto
                {
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages,
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return HandleError(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("my-notes")]
    public async Task<IActionResult> GetMyNotes(CancellationToken cancellationToken)
    {
        var result = await _listUseCase.GetMyNotesAsync(cancellationToken);
        if (result.IsSuccess)
        {
            var response = new TeacherStudentNoteListResponse
            {
                Data = result.Data ?? Array.Empty<TeacherStudentNoteDto>(),
                Meta = new PagedMetaDto
                {
                    Page = 1,
                    PageSize = result.TotalItems > 0 ? result.TotalItems : 50,
                    TotalItems = result.TotalItems,
                    TotalPages = 1,
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return HandleError(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("{noteId:guid}")]
    public async Task<IActionResult> GetNote(Guid noteId, CancellationToken cancellationToken)
    {
        var result = await _getUseCase.ExecuteAsync(noteId, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new TeacherStudentNoteResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return HandleError(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("student/{studentId:guid}")]
    public async Task<IActionResult> GetNoteForStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _getUseCase.GetForStudentAsync(studentId, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new TeacherStudentNoteResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return HandleError(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNote([FromBody] CreateTeacherStudentNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _createUseCase.ExecuteAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new TeacherStudentNoteResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return CreatedAtAction(nameof(GetNote), new { noteId = Guid.Parse(result.Data!.NoteId) }, response);
        }

        return HandleError(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPut("{noteId:guid}")]
    public async Task<IActionResult> UpdateNote(Guid noteId, [FromBody] UpdateTeacherStudentNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _updateUseCase.ExecuteAsync(noteId, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new TeacherStudentNoteResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return HandleError(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPut("student/{studentId:guid}")]
    public async Task<IActionResult> UpsertNoteForStudent(Guid studentId, [FromBody] UpsertTeacherStudentNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _upsertUseCase.ExecuteAsync(studentId, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new TeacherStudentNoteResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return HandleError(result.ErrorCode, result.ErrorMessage);
    }

    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> DeleteNote(Guid noteId, [FromQuery] string? rowVersion, CancellationToken cancellationToken)
    {
        var result = await _deleteUseCase.ExecuteAsync(noteId, rowVersion, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return HandleError(result.ErrorCode, result.ErrorMessage);
    }

    private IActionResult HandleError(string? errorCode, string? errorMessage)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        if (errorCode == ErrorCodes.ValidationFailed)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu đầu vào không hợp lệ.",
                Detail = errorMessage ?? "Vui lòng kiểm tra lại thông tin cung cấp.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            });
        }

        if (errorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                Status = StatusCodes.Status404NotFound,
                Title = "Không tìm thấy dữ liệu.",
                Detail = errorMessage ?? "Không tìm thấy ghi chú hoặc học sinh tương ứng.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            });
        }

        if (errorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                Status = StatusCodes.Status403Forbidden,
                Title = "Bạn không có quyền truy cập.",
                Detail = errorMessage ?? "Tài khoản của bạn không được phép thực hiện thao tác này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            });
        }

        if (errorCode == ErrorCodes.ConcurrencyConflict)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                Status = StatusCodes.Status409Conflict,
                Title = "Dữ liệu đã bị thay đổi.",
                Detail = errorMessage ?? "Thông tin ghi chú đã được cập nhật bởi một phiên khác. Vui lòng tải lại trang.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            });
        }

        return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Đã xảy ra lỗi máy chủ.",
            Detail = errorMessage,
            Instance = HttpContext.Request.Path,
            Extensions = { ["traceId"] = traceId }
        });
    }
}
