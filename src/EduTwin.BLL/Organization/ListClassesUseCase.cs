using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduTwin.BLL.Organization;

public class ListClassesUseCase : IListClassesUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ListClassesUseCase> _logger;

    public ListClassesUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<ListClassesUseCase> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ListClassesResult> ExecuteAsync(ClassListQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty || _tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
        {
            return ListClassesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager))
        {
            return ListClassesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var validationContext = new ValidationContext(query);
        var validationResults = new System.Collections.Generic.List<ValidationResult>();
        if (!Validator.TryValidateObject(query, validationContext, validationResults, true))
        {
            return ListClassesResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (query.Status.HasValue && !Enum.IsDefined(typeof(ClassStatus), query.Status.Value))
        {
            return ListClassesResult.Failure(ErrorCodes.ValidationFailed);
        }

        var centerId = _tenantContext.CenterId.Value;

        var centerExists = await _dbContext.Centers
            .AnyAsync(c => c.CenterId == centerId && c.Status == CenterStatus.Active, cancellationToken);

        if (!centerExists)
        {
            return ListClassesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var dbQuery = _dbContext.Classes
            .AsNoTracking()
            .Where(c => c.CenterId == centerId && !c.IsDeleted &&
                        c.Subject != null && c.Subject.CenterId == centerId && !c.Subject.IsDeleted &&
                        c.Teacher != null && c.Teacher.CenterId == centerId && !c.Teacher.IsDeleted &&
                        c.Teacher.User != null && c.Teacher.User.CenterId == centerId && !c.Teacher.User.IsDeleted &&
                        c.Teacher.User.RoleName == UserRole.Teacher);

        if (_tenantContext.Role == nameof(UserRole.Teacher))
        {
            dbQuery = dbQuery.Where(c => c.TeacherId == _tenantContext.UserId.Value);
        }

        if (query.TeacherId.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.TeacherId == query.TeacherId.Value);
        }

        if (query.SubjectId.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.SubjectId == query.SubjectId.Value);
        }

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.Status == query.Status.Value);
        }

        var totalItems = await dbQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)query.PageSize);

        var items = await dbQuery
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.ClassId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new ClassDto
            {
                ClassId = c.ClassId.ToString("D").ToLowerInvariant(),
                ClassName = c.ClassName,
                AcademicYear = c.AcademicYear,
                Subject = new ClassSubjectDto
                {
                    SubjectId = c.SubjectId.ToString("D").ToLowerInvariant(),
                    SubjectName = c.Subject.SubjectName
                },
                Teacher = new ClassTeacherDto
                {
                    TeacherId = c.TeacherId.ToString("D").ToLowerInvariant(),
                    DisplayName = c.Teacher.User.DisplayName
                },
                StudentCount = c.ClassStudents.Count(cs => cs.CenterId == centerId && cs.Status == ClassStudentStatus.Active),
                Status = c.Status.ToString(),
                RowVersion = c.RowVersion.ToString(CultureInfo.InvariantCulture)
            })
            .ToListAsync(cancellationToken);

        return ListClassesResult.Success(items, totalItems, totalPages);
    }
}
