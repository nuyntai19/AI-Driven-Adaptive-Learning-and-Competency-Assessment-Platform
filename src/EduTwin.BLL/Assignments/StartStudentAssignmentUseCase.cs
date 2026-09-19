using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.DAL;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.Assignments;

public class StartStudentAssignmentUseCase : IStartStudentAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IGetStudentAssignmentUseCase _getStudentAssignmentUseCase;
    private readonly TimeProvider _timeProvider;

    public StartStudentAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IGetStudentAssignmentUseCase getStudentAssignmentUseCase,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _getStudentAssignmentUseCase = getStudentAssignmentUseCase;
        _timeProvider = timeProvider;
    }

    public async Task<GetStudentAssignmentResult> ExecuteAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.Student), StringComparison.Ordinal))
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var currentUserId = _tenantContext.UserId;
        var centerId = _tenantContext.CenterId;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var progress = await _dbContext.StudentAssignmentProgresses
            .FirstOrDefaultAsync(p =>
                p.CenterId == centerId &&
                p.StudentId == currentUserId &&
                p.AssignmentId == assignmentId &&
                !p.IsDeleted, cancellationToken);

        if (progress == null)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (progress.StartedAt == null)
        {
            progress.StartedAt = utcNow;
            if (progress.Status == ProgressStatus.NotStarted)
            {
                progress.Status = ProgressStatus.InProgress;
            }
            progress.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await _getStudentAssignmentUseCase.ExecuteAsync(assignmentId, cancellationToken);
    }
}
