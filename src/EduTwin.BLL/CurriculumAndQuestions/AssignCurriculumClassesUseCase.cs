using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class AssignCurriculumClassesUseCase : IAssignCurriculumClassesUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public AssignCurriculumClassesUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<AssignCurriculumClassesResult> ExecuteAsync(Guid curriculumId, AssignCurriculumClassesRequest request, CancellationToken cancellationToken = default)
    {
        if (!CurriculumGuards.TryResolveActor(_tenantContext, out var centerId, out var actorId, out var isTeacher))
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (request.ClassIds == null || !CurriculumGuards.TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ValidationFailed);
        }

        var curriculum = await _dbContext.Curriculums
            .FirstOrDefaultAsync(c => c.CurriculumId == curriculumId &&
                                      c.CenterId == centerId &&
                                      !c.IsDeleted, cancellationToken);

        if (curriculum == null)
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!CurriculumGuards.CanAccess(curriculum, actorId, isTeacher))
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (curriculum.RowVersion != rowVersion)
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        if (curriculum.ReviewStatus != ReviewStatus.Draft)
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.InvalidStateTransition);
        }

        var distinctClassIds = request.ClassIds.Distinct().ToList();
        if (distinctClassIds.Count != request.ClassIds.Count)
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (distinctClassIds.Count > 0)
        {
            var dbClassesCount = await _dbContext.Classes
                .Where(c => c.CenterId == centerId &&
                            c.SubjectId == curriculum.SubjectId &&
                            c.Status == ClassStatus.Active &&
                            !c.IsDeleted &&
                            distinctClassIds.Contains(c.ClassId))
                .CountAsync(cancellationToken);

            if (dbClassesCount != distinctClassIds.Count)
            {
                return AssignCurriculumClassesResult.Failure(ErrorCodes.ResourceNotFound);
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var existingClasses = await _dbContext.CurriculumClasses
            .Where(cc => cc.CurriculumId == curriculumId && cc.CenterId == centerId)
            .ToListAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.CurriculumClasses.RemoveRange(existingClasses);

            var newClasses = distinctClassIds.Select(classId => new CurriculumClass
            {
                CenterId = centerId,
                CurriculumId = curriculumId,
                ClassId = classId,
                AssignedAt = now,
                AssignedBy = actorId
            }).ToList();

            _dbContext.CurriculumClasses.AddRange(newClasses);

            curriculum.UpdatedAt = now;
            curriculum.UpdatedBy = actorId;
            curriculum.RowVersion++;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var nodes = await _dbContext.CurriculumNodes
            .AsNoTracking()
            .Where(cn => cn.CurriculumId == curriculumId && cn.CenterId == centerId)
            .OrderBy(cn => cn.OrderIndex)
            .Select(cn => cn.NodeId)
            .ToListAsync(cancellationToken);

        var dto = new CurriculumDto
        {
            CurriculumId = curriculum.CurriculumId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            TeacherId = curriculum.TeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            SubjectId = curriculum.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = curriculum.Title,
            Description = curriculum.Description,
            SourceFile = curriculum.SourceFile,
            ReviewStatus = curriculum.ReviewStatus.ToString(),
            ClassIds = distinctClassIds.OrderBy(id => id).Select(id => id.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant()).ToList(),
            NodeIds = nodes.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList(),
            RowVersion = curriculum.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return AssignCurriculumClassesResult.Success(dto);
    }
}
