using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class UpsertTeacherStudentNoteUseCase : IUpsertTeacherStudentNoteUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _ownershipGuard;
    private readonly TimeProvider _timeProvider;

    public UpsertTeacherStudentNoteUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard ownershipGuard,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _ownershipGuard = ownershipGuard;
        _timeProvider = timeProvider;
    }

    public async Task<TeacherStudentNoteResult> ExecuteAsync(Guid studentId, UpsertTeacherStudentNoteRequest request, CancellationToken cancellationToken)
    {
        if (studentId == Guid.Empty || request == null)
            return TeacherStudentNoteResult.ValidationFailed("Mã học sinh và thông tin ghi chú không hợp lệ.");

        if (request.Content != null && request.Content.Length > 4000)
            return TeacherStudentNoteResult.ValidationFailed("Nội dung ghi chú tối đa 4000 ký tự.");

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return TeacherStudentNoteResult.Forbidden();

        var centerId = _tenantContext.CenterId.Value;
        var userId = _tenantContext.UserId.Value;

        // Verify student access
        var decision = await _ownershipGuard.CheckStudentAccessAsync(studentId, cancellationToken);
        if (decision == OwnershipDecision.NotFound)
            return TeacherStudentNoteResult.NotFound("Không tìm thấy học sinh.");
        if (decision == OwnershipDecision.Forbidden)
            return TeacherStudentNoteResult.Forbidden("Bạn không có quyền truy cập học sinh này.");

        // Resolve teacher id
        Guid teacherId;
        if (_tenantContext.Role == nameof(UserRole.Teacher))
        {
            teacherId = userId;
        }
        else if (_tenantContext.Role == nameof(UserRole.CenterManager))
        {
            var teacherExists = await _dbContext.Teachers
                .AsNoTracking()
                .AnyAsync(t => t.TeacherId == userId, cancellationToken);

            if (teacherExists)
            {
                teacherId = userId;
            }
            else
            {
                var anyTeacher = await _dbContext.Teachers
                    .AsNoTracking()
                    .Select(t => t.TeacherId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (anyTeacher == Guid.Empty)
                    return TeacherStudentNoteResult.ValidationFailed("Chưa có giáo viên nào trong trung tâm để gán ghi chú.");
                teacherId = anyTeacher;
            }
        }
        else
        {
            return TeacherStudentNoteResult.Forbidden();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Find existing active note for this teacher and student
        var existingNote = await _dbContext.TeacherStudentNotes
            .FirstOrDefaultAsync(n => n.StudentId == studentId && n.TeacherId == teacherId, cancellationToken);

        if (existingNote != null)
        {
            // Concurrency check if rowVersion passed
            if (!string.IsNullOrWhiteSpace(request.RowVersion))
            {
                if (ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var clientVersion))
                {
                    if (existingNote.RowVersion != clientVersion)
                        return TeacherStudentNoteResult.ConcurrencyConflict("Ghi chú đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại trang.");
                }
            }

            existingNote.Content = (request.Content ?? string.Empty).Trim();
            existingNote.UpdatedAt = now;
            existingNote.UpdatedBy = userId;
            existingNote.RowVersion++;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var student = await _dbContext.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);
            var teacher = await _dbContext.Teachers.Include(t => t.User).AsNoTracking().FirstOrDefaultAsync(t => t.TeacherId == teacherId, cancellationToken);

            return TeacherStudentNoteResult.Success(new TeacherStudentNoteDto
            {
                NoteId = existingNote.NoteId.ToString(),
                TeacherId = existingNote.TeacherId.ToString(),
                TeacherName = teacher?.User?.DisplayName,
                StudentId = existingNote.StudentId.ToString(),
                StudentName = student?.FullName,
                Content = existingNote.Content,
                CreatedAt = existingNote.CreatedAt,
                UpdatedAt = existingNote.UpdatedAt,
                RowVersion = existingNote.RowVersion.ToString()
            });
        }
        else
        {
            var newNote = new TeacherStudentNote
            {
                NoteId = Guid.NewGuid(),
                CenterId = centerId,
                TeacherId = teacherId,
                StudentId = studentId,
                Content = (request.Content ?? string.Empty).Trim(),
                CreatedAt = now,
                CreatedBy = userId,
                UpdatedAt = now,
                UpdatedBy = userId,
                IsDeleted = false,
                RowVersion = 1ul
            };

            _dbContext.TeacherStudentNotes.Add(newNote);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var student = await _dbContext.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == studentId, cancellationToken);
            var teacher = await _dbContext.Teachers.Include(t => t.User).AsNoTracking().FirstOrDefaultAsync(t => t.TeacherId == teacherId, cancellationToken);

            return TeacherStudentNoteResult.Success(new TeacherStudentNoteDto
            {
                NoteId = newNote.NoteId.ToString(),
                TeacherId = newNote.TeacherId.ToString(),
                TeacherName = teacher?.User?.DisplayName,
                StudentId = newNote.StudentId.ToString(),
                StudentName = student?.FullName,
                Content = newNote.Content,
                CreatedAt = newNote.CreatedAt,
                UpdatedAt = newNote.UpdatedAt,
                RowVersion = newNote.RowVersion.ToString()
            });
        }
    }
}
