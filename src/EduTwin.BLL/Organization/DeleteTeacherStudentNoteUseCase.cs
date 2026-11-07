using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class DeleteTeacherStudentNoteUseCase : IDeleteTeacherStudentNoteUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public DeleteTeacherStudentNoteUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<DeleteTeacherStudentNoteResult> ExecuteAsync(Guid noteId, string? rowVersion, CancellationToken cancellationToken)
    {
        if (noteId == Guid.Empty)
            return DeleteTeacherStudentNoteResult.ValidationFailed("Mã ghi chú không hợp lệ.");

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return DeleteTeacherStudentNoteResult.Forbidden();

        var userId = _tenantContext.UserId.Value;

        var note = await _dbContext.TeacherStudentNotes
            .FirstOrDefaultAsync(n => n.NoteId == noteId, cancellationToken);

        if (note == null)
            return DeleteTeacherStudentNoteResult.NotFound("Không tìm thấy ghi chú.");

        // Authorization: only author teacher or center manager
        if (_tenantContext.Role == nameof(UserRole.Teacher) && note.TeacherId != userId)
            return DeleteTeacherStudentNoteResult.Forbidden("Bạn không có quyền xóa ghi chú của giáo viên khác.");

        if (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager))
            return DeleteTeacherStudentNoteResult.Forbidden();

        // Concurrency check
        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            if (ulong.TryParse(rowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var clientVersion))
            {
                if (note.RowVersion != clientVersion)
                    return DeleteTeacherStudentNoteResult.ConcurrencyConflict("Ghi chú đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại trang.");
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        note.IsDeleted = true;
        note.DeletedAt = now;
        note.DeletedBy = userId;
        note.RowVersion++;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return DeleteTeacherStudentNoteResult.Success();
    }
}
