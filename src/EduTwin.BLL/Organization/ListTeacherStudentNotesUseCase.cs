using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class ListTeacherStudentNotesUseCase : IListTeacherStudentNotesUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _ownershipGuard;

    public ListTeacherStudentNotesUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard ownershipGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _ownershipGuard = ownershipGuard;
    }

    public async Task<ListTeacherStudentNotesResult> ExecuteAsync(TeacherStudentNoteListQuery query, CancellationToken cancellationToken)
    {
        if (query == null)
            return ListTeacherStudentNotesResult.ValidationFailed("Truy vấn không hợp lệ.");

        if (query.Page <= 0 || query.PageSize <= 0 || query.PageSize > 100)
            return ListTeacherStudentNotesResult.ValidationFailed("Trang hoặc kích thước trang không hợp lệ.");

        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return ListTeacherStudentNotesResult.Forbidden();

        var userId = _tenantContext.UserId.Value;

        if (query.StudentId.HasValue && query.StudentId.Value != Guid.Empty)
        {
            var decision = await _ownershipGuard.CheckStudentAccessAsync(query.StudentId.Value, cancellationToken);
            if (decision == OwnershipDecision.NotFound)
                return ListTeacherStudentNotesResult.NotFound("Không tìm thấy học sinh.");
            if (decision == OwnershipDecision.Forbidden)
                return ListTeacherStudentNotesResult.Forbidden("Bạn không có quyền truy cập học sinh này.");
        }

        var baseQuery = _dbContext.TeacherStudentNotes
            .Include(n => n.Student)
            .Include(n => n.Teacher)
                .ThenInclude(t => t.User)
            .AsNoTracking();

        if (query.StudentId.HasValue && query.StudentId.Value != Guid.Empty)
        {
            baseQuery = baseQuery.Where(n => n.StudentId == query.StudentId.Value);
        }

        if (query.TeacherId.HasValue && query.TeacherId.Value != Guid.Empty)
        {
            baseQuery = baseQuery.Where(n => n.TeacherId == query.TeacherId.Value);
        }
        else if (_tenantContext.Role == nameof(UserRole.Teacher) && (!query.StudentId.HasValue || query.StudentId.Value == Guid.Empty))
        {
            // Default to teacher's own notes if no student specified
            baseQuery = baseQuery.Where(n => n.TeacherId == userId);
        }

        var totalItems = await baseQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalItems / query.PageSize);

        var items = await baseQuery
            .OrderByDescending(n => n.UpdatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => new TeacherStudentNoteDto
            {
                NoteId = n.NoteId.ToString(),
                TeacherId = n.TeacherId.ToString(),
                TeacherName = n.Teacher != null && n.Teacher.User != null ? n.Teacher.User.DisplayName : null,
                StudentId = n.StudentId.ToString(),
                StudentName = n.Student != null ? n.Student.FullName : null,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                RowVersion = n.RowVersion.ToString()
            })
            .ToListAsync(cancellationToken);

        return ListTeacherStudentNotesResult.Success(items, totalItems, totalPages);
    }

    public async Task<ListTeacherStudentNotesResult> GetMyNotesAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
            return ListTeacherStudentNotesResult.Forbidden();

        var userId = _tenantContext.UserId.Value;

        var items = await _dbContext.TeacherStudentNotes
            .Include(n => n.Student)
            .Include(n => n.Teacher)
                .ThenInclude(t => t.User)
            .AsNoTracking()
            .Where(n => n.TeacherId == userId)
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => new TeacherStudentNoteDto
            {
                NoteId = n.NoteId.ToString(),
                TeacherId = n.TeacherId.ToString(),
                TeacherName = n.Teacher != null && n.Teacher.User != null ? n.Teacher.User.DisplayName : null,
                StudentId = n.StudentId.ToString(),
                StudentName = n.Student != null ? n.Student.FullName : null,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                RowVersion = n.RowVersion.ToString()
            })
            .ToListAsync(cancellationToken);

        return ListTeacherStudentNotesResult.Success(items, items.Count, 1);
    }
}
