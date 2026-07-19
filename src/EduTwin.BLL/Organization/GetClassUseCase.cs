using System;
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
using EduTwin.DAL.IdentityAndTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduTwin.BLL.Organization;

public class GetClassUseCase : IGetClassUseCase
{
    private readonly EduTwinDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetClassUseCase> _logger;
    private readonly IClassOwnershipGuard _ownershipGuard;

    public GetClassUseCase(
        EduTwinDbContext context,
        ITenantContext tenantContext,
        ILogger<GetClassUseCase> logger,
        IClassOwnershipGuard ownershipGuard)
    {
        _context = context;
        _tenantContext = tenantContext;
        _logger = logger;
        _ownershipGuard = ownershipGuard;
    }

    public async Task<GetClassResult> ExecuteAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        if (classId == Guid.Empty)
        {
            _logger.LogWarning("ClassId is empty.");
            return GetClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId == null ||
            _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == null ||
            _tenantContext.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            _logger.LogWarning("Tenant context is invalid.");
            return GetClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var role = _tenantContext.Role;
        if (role != nameof(UserRole.CenterManager) && role != nameof(UserRole.Teacher))
        {
            _logger.LogWarning("Role {Role} is not CenterManager or Teacher.", role);
            return GetClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var decision = await _ownershipGuard.CheckClassAccessAsync(classId, cancellationToken);
        if (decision == OwnershipDecision.NotFound)
        {
            return GetClassResult.Failure(ErrorCodes.ResourceNotFound);
        }
        if (decision == OwnershipDecision.Forbidden)
        {
            return GetClassResult.Failure(ErrorCodes.ForbiddenResource);
        }
        if (decision != OwnershipDecision.Allowed)
        {
            return GetClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;

        var classDto = await _context.Classes
            .AsNoTracking()
            .Where(c => c.ClassId == classId && c.CenterId == centerId && !c.IsDeleted &&
                        c.Subject != null && c.Subject.CenterId == centerId && !c.Subject.IsDeleted &&
                        c.Teacher != null && c.Teacher.CenterId == centerId && !c.Teacher.IsDeleted &&
                        c.Teacher.User != null && c.Teacher.User.CenterId == centerId && !c.Teacher.User.IsDeleted &&
                        c.Teacher.User.RoleName == UserRole.Teacher)
            .Select(c => new
            {
                c.ClassId,
                c.ClassName,
                c.AcademicYear,
                c.Status,
                RowVersion = c.RowVersion,
                Subject = new { c.Subject.SubjectId, c.Subject.SubjectName },
                Teacher = new { c.Teacher.TeacherId, User = new { c.Teacher.User.DisplayName } },
                StudentCount = _context.ClassStudents.Count(cs => cs.ClassId == c.ClassId && cs.CenterId == centerId && cs.Status == ClassStudentStatus.Active)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (classDto == null)
        {
            return GetClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var dto = new ClassDto
        {
            ClassId = classDto.ClassId.ToString("D").ToLowerInvariant(),
            ClassName = classDto.ClassName,
            AcademicYear = classDto.AcademicYear,
            Subject = new ClassSubjectDto
            {
                SubjectId = classDto.Subject.SubjectId.ToString("D").ToLowerInvariant(),
                SubjectName = classDto.Subject.SubjectName
            },
            Teacher = new ClassTeacherDto
            {
                TeacherId = classDto.Teacher.TeacherId.ToString("D").ToLowerInvariant(),
                DisplayName = classDto.Teacher.User.DisplayName
            },
            Status = classDto.Status.ToString(),
            StudentCount = classDto.StudentCount,
            RowVersion = classDto.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return GetClassResult.Success(dto);
    }
}
