using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class UpdateTeacherStudentNoteUseCase : IUpdateTeacherStudentNoteUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public UpdateTeacherStudentNoteUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<TeacherStudentNoteResult> ExecuteAsync(Guid noteId, UpdateTeacherStudentNoteRequest request, CancellationToken cancellationToken)
    {
        if (noteId == Guid.Empty || request == null || string.IsNullOrWhiteSpace(request.Content))
            return TeacherStudentNoteResult.ValidationFailed("Mã ghi chú và nội dung không được để trống.");

        if (request.Content.Length > 4000)
            return TeacherStudentNoteResult.ValidationFailed("Nội dung ghi chú tối đa 4000 ký tự.");

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return TeacherStudentNoteResult.Forbidden();

        var userId = _tenantContext.UserId.Value;

        var note = await _dbContext.TeacherStudentNotes
            .Include(n => n.Student)
            .Include(n => n.Teacher)
                .ThenInclude(t => t.User)
            .FirstOrDefaultAsync(n => n.NoteId == noteId, cancellationToken);

        if (note == null)
            return TeacherStudentNoteResult.NotFound("Không tìm thấy ghi chú.");

        // Authorization: only the author teacher or center manager can update
        if (_tenantContext.Role == nameof(UserRole.Teacher) && note.TeacherId != userId)
            return TeacherStudentNoteResult.Forbidden("Bạn không có quyền chỉnh sửa ghi chú của giáo viên khác.");

        if (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager))
            return TeacherStudentNoteResult.Forbidden();

        // Concurrency check
        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            if (ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var clientVersion))
            {
                if (note.RowVersion != clientVersion)
                    return TeacherStudentNoteResult.ConcurrencyConflict("Ghi chú đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại trang.");
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        note.Content = request.Content.Trim();
        note.UpdatedAt = now;
        note.UpdatedBy = userId;
        note.RowVersion++;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return TeacherStudentNoteResult.Success(new TeacherStudentNoteDto
        {
            NoteId = note.NoteId.ToString(),
            TeacherId = note.TeacherId.ToString(),
            TeacherName = note.Teacher?.User?.DisplayName,
            StudentId = note.StudentId.ToString(),
            StudentName = note.Student?.FullName,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
            RowVersion = note.RowVersion.ToString()
        });
    }
}
