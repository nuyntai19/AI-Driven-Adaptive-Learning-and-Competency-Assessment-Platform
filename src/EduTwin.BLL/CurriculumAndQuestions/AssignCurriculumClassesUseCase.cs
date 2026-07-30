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
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;

        if (request.ClassIds == null || string.IsNullOrWhiteSpace(request.RowVersion) || !ulong.TryParse(request.RowVersion, out var rowVersion))
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

        if (curriculum.RowVersion != rowVersion || curriculum.ReviewStatus != ReviewStatus.Draft)
        {
            return AssignCurriculumClassesResult.Failure(ErrorCodes.ValidationFailed); 
        }

        var distinctClassIds = request.ClassIds.Distinct().ToList();

        if (distinctClassIds.Count > 0)
        {
            var dbClassesCount = await _dbContext.Classes
                .Where(c => c.CenterId == centerId && !c.IsDeleted && distinctClassIds.Contains(c.ClassId))
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
                ClassId = classId
            }).ToList();

            _dbContext.CurriculumClasses.AddRange(newClasses);

            curriculum.UpdatedAt = now;
            curriculum.UpdatedBy = actorId;
            curriculum.RowVersion++;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
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
            ClassIds = distinctClassIds.Select(id => id.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant()).ToList(),
            NodeIds = nodes.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList(),
            RowVersion = curriculum.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return AssignCurriculumClassesResult.Success(dto);
    }
}
