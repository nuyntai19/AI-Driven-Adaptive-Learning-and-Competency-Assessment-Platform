using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class RemoveStudentFromClassUseCase : IRemoveStudentFromClassUseCase
{
    private readonly EduTwinDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly IClassOwnershipGuard _ownershipGuard;
    private readonly ILogger<RemoveStudentFromClassUseCase> _logger;

    public RemoveStudentFromClassUseCase(
        EduTwinDbContext context,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        IClassOwnershipGuard ownershipGuard,
        ILogger<RemoveStudentFromClassUseCase> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _ownershipGuard = ownershipGuard;
        _logger = logger;
    }

    public async Task<RemoveStudentFromClassResult> ExecuteAsync(
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (classId == Guid.Empty || studentId == Guid.Empty)
        {
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId == null ||
            _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == null ||
            _tenantContext.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.Role != nameof(EduTwin.Contracts.IdentityAndTenancy.UserRole.Teacher) &&
            _tenantContext.Role != nameof(EduTwin.Contracts.IdentityAndTenancy.UserRole.CenterManager))
        {
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;

        var centerExists = await _context.Centers
            .AnyAsync(c => c.CenterId == centerId && c.Status == EduTwin.Contracts.Organization.CenterStatus.Active && !c.IsDeleted, cancellationToken);

        if (!centerExists)
        {
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var ownershipDecision = await _ownershipGuard.CheckClassAccessAsync(classId, cancellationToken);
        if (ownershipDecision == OwnershipDecision.Forbidden)
        {
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ForbiddenResource);
        }
        if (ownershipDecision == OwnershipDecision.NotFound || ownershipDecision != OwnershipDecision.Allowed)
        {
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var membership = await _context.ClassStudents
            .FirstOrDefaultAsync(cs => cs.CenterId == centerId && cs.ClassId == classId && cs.StudentId == studentId, cancellationToken);

        if (membership == null)
        {
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (membership.Status == EduTwin.Contracts.Organization.ClassStudentStatus.Removed)
        {
            return RemoveStudentFromClassResult.Success();
        }

        if (membership.Status != EduTwin.Contracts.Organization.ClassStudentStatus.Active)
        {
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        membership.Status = EduTwin.Contracts.Organization.ClassStudentStatus.Removed;
        membership.RemovedAt = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return RemoveStudentFromClassResult.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict when removing student {StudentId} from class {ClassId}", studentId, classId);
            _context.ChangeTracker.Clear();
            return RemoveStudentFromClassResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
    }
}
