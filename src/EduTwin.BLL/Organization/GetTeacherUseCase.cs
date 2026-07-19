using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class GetTeacherUseCase : IGetTeacherUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ITeacherOwnershipGuard _teacherOwnershipGuard;

    public GetTeacherUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        ITeacherOwnershipGuard teacherOwnershipGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _teacherOwnershipGuard = teacherOwnershipGuard;
    }

    public async Task<GetTeacherResult> ExecuteAsync(Guid teacherId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
        {
            return GetTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (teacherId == Guid.Empty)
        {
            return GetTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var accessResult = await _teacherOwnershipGuard.CheckTeacherAccessAsync(teacherId, cancellationToken);
        switch (accessResult)
        {
            case OwnershipDecision.Allowed:
                break;
            case OwnershipDecision.Forbidden:
                return GetTeacherResult.Failure(ErrorCodes.ForbiddenResource);
            case OwnershipDecision.NotFound:
            default:
                return GetTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;

        var teacher = await _dbContext.Teachers
            .AsNoTracking()
            .Where(t => t.CenterId == centerId && !t.IsDeleted && t.TeacherId == teacherId)
            .Where(t => t.User != null && t.User.CenterId == centerId && !t.User.IsDeleted && t.User.RoleName == UserRole.Teacher)
            .Select(t => new TeacherDto
            {
                TeacherId = t.TeacherId.ToString("D").ToLowerInvariant(),
                Username = t.User!.Username,
                DisplayName = t.User.DisplayName,
                Department = t.Department,
                Status = t.User.Status.ToString(),
                ClassCount = _dbContext.Classes.Count(c => c.CenterId == centerId && c.TeacherId == t.TeacherId && !c.IsDeleted && c.Status == ClassStatus.Active),
                RowVersion = t.RowVersion.ToString(CultureInfo.InvariantCulture)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (teacher == null)
        {
            return GetTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        return GetTeacherResult.Success(teacher);
    }
}
