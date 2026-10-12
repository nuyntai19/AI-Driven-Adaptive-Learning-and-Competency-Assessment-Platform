using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class GetTeacherStudentNoteUseCase : IGetTeacherStudentNoteUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _ownershipGuard;

    public GetTeacherStudentNoteUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard ownershipGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _ownershipGuard = ownershipGuard;
    }

    public async Task<TeacherStudentNoteResult> ExecuteAsync(Guid noteId, CancellationToken cancellationToken)
    {
        if (noteId == Guid.Empty)
            return TeacherStudentNoteResult.ValidationFailed("Mã ghi chú không hợp lệ.");

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return TeacherStudentNoteResult.Forbidden();

        var note = await _dbContext.TeacherStudentNotes
            .Include(n => n.Student)
            .Include(n => n.Teacher)
                .ThenInclude(t => t.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NoteId == noteId, cancellationToken);

        if (note == null)
            return TeacherStudentNoteResult.NotFound("Không tìm thấy ghi chú.");

        var decision = await _ownershipGuard.CheckStudentAccessAsync(note.StudentId, cancellationToken);
        if (decision == OwnershipDecision.Forbidden)
            return TeacherStudentNoteResult.Forbidden("Bạn không có quyền truy cập ghi chú này.");

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

    public async Task<TeacherStudentNoteResult> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (studentId == Guid.Empty)
            return TeacherStudentNoteResult.ValidationFailed("Mã học sinh không hợp lệ.");

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return TeacherStudentNoteResult.Forbidden();

        var userId = _tenantContext.UserId.Value;

        var decision = await _ownershipGuard.CheckStudentAccessAsync(studentId, cancellationToken);
        if (decision == OwnershipDecision.NotFound)
            return TeacherStudentNoteResult.NotFound("Không tìm thấy học sinh.");
        if (decision == OwnershipDecision.Forbidden)
            return TeacherStudentNoteResult.Forbidden("Bạn không có quyền truy cập học sinh này.");

        var query = _dbContext.TeacherStudentNotes
            .Include(n => n.Student)
            .Include(n => n.Teacher)
                .ThenInclude(t => t.User)
            .AsNoTracking()
            .Where(n => n.StudentId == studentId);

        if (_tenantContext.Role == nameof(UserRole.Teacher))
        {
            query = query.Where(n => n.TeacherId == userId);
        }

        var note = await query.OrderByDescending(n => n.UpdatedAt).FirstOrDefaultAsync(cancellationToken);

        if (note == null)
            return TeacherStudentNoteResult.NotFound("Chưa có ghi chú nào cho học sinh này.");

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
