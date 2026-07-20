using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class AddStudentsToClassUseCase : IAddStudentsToClassUseCase
{
    private readonly EduTwinDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly IClassOwnershipGuard _classOwnershipGuard;
    private readonly ILogger<AddStudentsToClassUseCase> _logger;

    public AddStudentsToClassUseCase(
        EduTwinDbContext context,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        IClassOwnershipGuard classOwnershipGuard,
        ILogger<AddStudentsToClassUseCase> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _classOwnershipGuard = classOwnershipGuard;
        _logger = logger;
    }

    public async Task<AddStudentsToClassResult> ExecuteAsync(Guid classId, AddStudentsToClassRequest request, CancellationToken cancellationToken = default)
    {
        if (classId == Guid.Empty)
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request == null || request.StudentIds == null || request.StudentIds.Count == 0)
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.StudentIds.Any(id => id == Guid.Empty))
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.StudentIds.Distinct().Count() != request.StudentIds.Count)
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId == null ||
            _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == null ||
            _tenantContext.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.Role != nameof(UserRole.CenterManager) &&
            _tenantContext.Role != nameof(UserRole.Teacher))
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var decision = await _classOwnershipGuard.CheckClassAccessAsync(classId, cancellationToken);
        switch (decision)
        {
            case OwnershipDecision.Allowed:
                break;
            case OwnershipDecision.Forbidden:
                return AddStudentsToClassResult.Failure(ErrorCodes.ForbiddenResource);
            case OwnershipDecision.NotFound:
            default:
                return AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var currentUserId = _tenantContext.UserId.Value;

        var existingClass = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId && c.ClassId == classId && !c.IsDeleted, cancellationToken);

        if (existingClass == null)
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var center = await _context.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId && !c.IsDeleted, cancellationToken);

        if (center == null || center.Status != CenterStatus.Active)
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var requestedStudentIds = request.StudentIds;

        var validStudents = await _context.Students
            .Include(s => s.User)
            .Where(s => requestedStudentIds.Contains(s.StudentId) && s.CenterId == centerId && !s.IsDeleted && !s.User.IsDeleted && s.User.RoleName == UserRole.Student)
            .ToListAsync(cancellationToken);

        if (validStudents.Any(s => s.User.Status != UserStatus.Active && s.User.Status != UserStatus.Locked && s.User.Status != UserStatus.Disabled))
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (validStudents.Count != requestedStudentIds.Count)
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var existingMemberships = await _context.ClassStudents
            .Where(cs => cs.CenterId == centerId && cs.ClassId == classId && requestedStudentIds.Contains(cs.StudentId))
            .ToListAsync(cancellationToken);

        if (existingMemberships.Any(cs => cs.Status != ClassStudentStatus.Active && cs.Status != ClassStudentStatus.Removed))
        {
            return AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        int addedCount = 0;
        int alreadyMemberCount = 0;
        bool hasChanges = false;
        var currentUtc = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var studentId in requestedStudentIds)
        {
            var membership = existingMemberships.FirstOrDefault(cs => cs.StudentId == studentId);

            if (membership == null)
            {
                _context.ClassStudents.Add(new ClassStudent
                {
                    CenterId = centerId,
                    ClassId = classId,
                    StudentId = studentId,
                    JoinedAt = currentUtc,
                    Status = ClassStudentStatus.Active,
                    RemovedAt = null,
                    CreatedBy = currentUserId
                });
                addedCount++;
                hasChanges = true;
            }
            else if (membership.Status == ClassStudentStatus.Active)
            {
                alreadyMemberCount++;
            }
            else if (membership.Status == ClassStudentStatus.Removed)
            {
                membership.Status = ClassStudentStatus.Active;
                membership.JoinedAt = currentUtc;
                membership.RemovedAt = null;
                membership.CreatedBy = currentUserId;
                addedCount++;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Concurrency or constraint error occurred while adding students to class {ClassId}", classId);
                throw; // Not eating the exception, rethrow to allow handling upstream.
            }
        }

        return AddStudentsToClassResult.Success(new AddStudentsToClassDto
        {
            ClassId = classId.ToString().ToLowerInvariant(),
            AddedCount = addedCount,
            AlreadyMemberCount = alreadyMemberCount
        });
    }
}
