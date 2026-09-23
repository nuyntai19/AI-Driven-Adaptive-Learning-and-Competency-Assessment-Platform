using System;
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

public class ArchiveCurriculumUseCase : IArchiveCurriculumUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public ArchiveCurriculumUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<ArchiveCurriculumResult> ExecuteAsync(Guid curriculumId, ArchiveCurriculumRequest request, CancellationToken cancellationToken = default)
    {
        if (!CurriculumGuards.TryResolveActor(_tenantContext, out var centerId, out var actorId, out var isTeacher))
        {
            return ArchiveCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!CurriculumGuards.TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return ArchiveCurriculumResult.Failure(ErrorCodes.ValidationFailed);
        }

        var curriculum = await _dbContext.Curriculums
            .FirstOrDefaultAsync(c => c.CurriculumId == curriculumId &&
                                      c.CenterId == centerId &&
                                      !c.IsDeleted, cancellationToken);

        if (curriculum == null)
        {
            return ArchiveCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!CurriculumGuards.CanAccess(curriculum, actorId, isTeacher))
        {
            return ArchiveCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (curriculum.RowVersion != rowVersion)
        {
            return ArchiveCurriculumResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        if (curriculum.ReviewStatus == ReviewStatus.Archived)
        {
            return ArchiveCurriculumResult.Failure(ErrorCodes.InvalidStateTransition);
        }

        curriculum.ReviewStatus = ReviewStatus.Archived;
        curriculum.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        curriculum.UpdatedBy = actorId;
        curriculum.RowVersion++;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ArchiveCurriculumResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        var nodes = await _dbContext.CurriculumNodes
            .AsNoTracking()
            .Where(cn => cn.CurriculumId == curriculumId && cn.CenterId == centerId)
            .OrderBy(cn => cn.OrderIndex)
            .Select(cn => cn.NodeId)
            .ToListAsync(cancellationToken);

        var classes = await _dbContext.CurriculumClasses
            .AsNoTracking()
            .Where(cc => cc.CurriculumId == curriculumId && cc.CenterId == centerId)
            .Select(cc => cc.ClassId)
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
            ClassIds = classes.Select(id => id.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant()).ToList(),
            NodeIds = nodes.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList(),
            RowVersion = curriculum.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return ArchiveCurriculumResult.Success(dto);
    }
}
