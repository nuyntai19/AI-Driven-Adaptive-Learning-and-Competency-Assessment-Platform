using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class CreateTeacherStudentNoteUseCase : ICreateTeacherStudentNoteUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _ownershipGuard;
    private readonly TimeProvider _timeProvider;

    public CreateTeacherStudentNoteUseCase(
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

    public async Task<TeacherStudentNoteResult> ExecuteAsync(CreateTeacherStudentNoteRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.StudentId == Guid.Empty || string.IsNullOrWhiteSpace(request.Content))
            return TeacherStudentNoteResult.ValidationFailed("Nội dung ghi chú và mã học sinh không được để trống.");

        if (request.Content.Length > 4000)
            return TeacherStudentNoteResult.ValidationFailed("Nội dung ghi chú tối đa 4000 ký tự.");

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return TeacherStudentNoteResult.Forbidden();

        var centerId = _tenantContext.CenterId.Value;
        var userId = _tenantContext.UserId.Value;

        // Verify student access
        var decision = await _ownershipGuard.CheckStudentAccessAsync(request.StudentId, cancellationToken);
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
            // CenterManager can act as teacher or uses manager user id
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
        var note = new TeacherStudentNote
        {
            NoteId = Guid.NewGuid(),
            CenterId = centerId,
            TeacherId = teacherId,
            StudentId = request.StudentId,
            Content = request.Content.Trim(),
            CreatedAt = now,
            CreatedBy = userId,
            UpdatedAt = now,
            UpdatedBy = userId,
            IsDeleted = false,
            RowVersion = 1ul
        };

        _dbContext.TeacherStudentNotes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var student = await _dbContext.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == request.StudentId, cancellationToken);
        var teacher = await _dbContext.Teachers.Include(t => t.User).AsNoTracking().FirstOrDefaultAsync(t => t.TeacherId == teacherId, cancellationToken);

        return TeacherStudentNoteResult.Success(new TeacherStudentNoteDto
        {
            NoteId = note.NoteId.ToString(),
            TeacherId = note.TeacherId.ToString(),
            TeacherName = teacher?.User?.DisplayName,
            StudentId = note.StudentId.ToString(),
            StudentName = student?.FullName,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
            RowVersion = note.RowVersion.ToString()
        });
    }
}
