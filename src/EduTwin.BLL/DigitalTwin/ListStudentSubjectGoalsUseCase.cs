using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin;

public class ListStudentSubjectGoalsUseCase : IListStudentSubjectGoalsUseCase
{
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _ownershipGuard;
    private readonly EduTwinDbContext _dbContext;

    public ListStudentSubjectGoalsUseCase(
        ITenantContext tenantContext,
        IStudentOwnershipGuard ownershipGuard,
        EduTwinDbContext dbContext)
    {
        _tenantContext = tenantContext;
        _ownershipGuard = ownershipGuard;
        _dbContext = dbContext;
    }

    public async Task<ListStudentSubjectGoalsResult> ExecuteAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            _tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty ||
            _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
            return ListStudentSubjectGoalsResult.Failure(ErrorCodes.ResourceNotFound);

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
            return ListStudentSubjectGoalsResult.Failure(ErrorCodes.ResourceNotFound);

        var role = _tenantContext.Role;
        if (!string.Equals(role, UserRole.Student.ToString(), StringComparison.Ordinal) &&
            !string.Equals(role, UserRole.Teacher.ToString(), StringComparison.Ordinal) &&
            !string.Equals(role, UserRole.CenterManager.ToString(), StringComparison.Ordinal))
            return ListStudentSubjectGoalsResult.Failure(ErrorCodes.ResourceNotFound);

        var centerId = _tenantContext.CenterId.Value;

        if (studentId == Guid.Empty)
            return ListStudentSubjectGoalsResult.Failure(ErrorCodes.ResourceNotFound);

        var centerExists = await _dbContext.Centers
            .AnyAsync(c => c.CenterId == centerId && !c.IsDeleted && c.Status == CenterStatus.Active, cancellationToken);
        if (!centerExists)
            return ListStudentSubjectGoalsResult.Failure(ErrorCodes.ResourceNotFound);

        var decision = await _ownershipGuard.CheckStudentAccessAsync(studentId, cancellationToken);
        switch (decision)
        {
            case OwnershipDecision.Allowed:
                break;
            case OwnershipDecision.Forbidden:
                return ListStudentSubjectGoalsResult.Failure(ErrorCodes.ForbiddenResource);
            case OwnershipDecision.NotFound:
            default:
                return ListStudentSubjectGoalsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var studentValid = await _dbContext.Students
            .Include(s => s.User)
            .AnyAsync(s => s.StudentId == studentId && s.CenterId == centerId && !s.IsDeleted
                        && s.User != null && s.User.CenterId == centerId && !s.User.IsDeleted && s.User.RoleName == UserRole.Student, cancellationToken);
        if (!studentValid)
            return ListStudentSubjectGoalsResult.Failure(ErrorCodes.ResourceNotFound);

        var goals = await _dbContext.StudentSubjectGoals
            .AsNoTracking()
            .Where(g => g.CenterId == centerId &&
                        g.StudentId == studentId &&
                        !g.IsDeleted &&
                        g.Subject.CenterId == centerId &&
                        !g.Subject.IsDeleted)
            .OrderBy(g => g.SubjectId)
            .ThenBy(g => g.GoalId)
            .Select(g => new StudentSubjectGoalDto
            {
                GoalId = g.GoalId.ToString(CultureInfo.InvariantCulture),
                StudentId = g.StudentId.ToString("D"),
                SubjectId = g.SubjectId.ToString("D"),
                TargetScore = g.TargetScore,
                RemainingDays = checked((int)g.RemainingDays),
                CurrentPredictedScore = g.CurrentPredictedScore,
                RiskScore = g.RiskScore,
                RowVersion = g.RowVersion.ToString(CultureInfo.InvariantCulture)
            })
            .ToListAsync(cancellationToken);

        return ListStudentSubjectGoalsResult.Success(goals);
    }
}
