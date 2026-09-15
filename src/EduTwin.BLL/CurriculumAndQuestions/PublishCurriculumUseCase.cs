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

public class PublishCurriculumUseCase : IPublishCurriculumUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public PublishCurriculumUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<PublishCurriculumResult> ExecuteAsync(Guid curriculumId, PublishCurriculumRequest request, CancellationToken cancellationToken = default)
    {
        if (!CurriculumGuards.TryResolveActor(_tenantContext, out var centerId, out var actorId, out var isTeacher))
        {
            return PublishCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!CurriculumGuards.TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return PublishCurriculumResult.Failure(ErrorCodes.ValidationFailed);
        }

        var curriculum = await _dbContext.Curriculums
            .FirstOrDefaultAsync(c => c.CurriculumId == curriculumId &&
                                      c.CenterId == centerId &&
                                      !c.IsDeleted, cancellationToken);

        if (curriculum == null)
        {
            return PublishCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!CurriculumGuards.CanAccess(curriculum, actorId, isTeacher))
        {
            return PublishCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (curriculum.RowVersion != rowVersion)
        {
            return PublishCurriculumResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        if (curriculum.ReviewStatus != ReviewStatus.Draft)
        {
            return PublishCurriculumResult.Failure(ErrorCodes.InvalidStateTransition);
        }

        curriculum.ReviewStatus = ReviewStatus.Published;
        curriculum.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        curriculum.UpdatedBy = actorId;
        curriculum.RowVersion++;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return PublishCurriculumResult.Failure(ErrorCodes.ConcurrencyConflict);
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

        return PublishCurriculumResult.Success(dto);
    }
}
