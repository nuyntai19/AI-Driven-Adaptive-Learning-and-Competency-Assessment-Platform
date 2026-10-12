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

public class UpdateCurriculumUseCase : IUpdateCurriculumUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public UpdateCurriculumUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateCurriculumResult> ExecuteAsync(Guid curriculumId, UpdateCurriculumRequest request, CancellationToken cancellationToken = default)
    {
        if (!CurriculumGuards.TryResolveActor(_tenantContext, out var centerId, out var actorId, out var isTeacher))
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!isTeacher)
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ForbiddenResource);
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 250)
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.GradeLevel.HasValue && (request.GradeLevel.Value < 10 || request.GradeLevel.Value > 12))
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!CurriculumGuards.TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ValidationFailed);
        }

        var curriculum = await _dbContext.Curriculums
            .FirstOrDefaultAsync(c => c.CurriculumId == curriculumId &&
                                      c.CenterId == centerId &&
                                      !c.IsDeleted, cancellationToken);

        if (curriculum == null)
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!CurriculumGuards.CanAccess(curriculum, actorId, isTeacher))
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (curriculum.RowVersion != rowVersion)
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        if (curriculum.ReviewStatus != ReviewStatus.Draft)
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.InvalidStateTransition);
        }

        var visibility = curriculum.Visibility;
        if (request.Visibility != null && (!Enum.TryParse(request.Visibility, out visibility) || !Enum.IsDefined(visibility) || visibility.ToString() != request.Visibility))
            return UpdateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

        curriculum.Visibility = visibility;
        curriculum.Title = request.Title;
        curriculum.Description = request.Description;
        curriculum.GradeLevel = request.GradeLevel;
        curriculum.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        curriculum.UpdatedBy = actorId;
        curriculum.RowVersion++;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UpdateCurriculumResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        // Map to DTO
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
            Visibility = curriculum.Visibility.ToString(),
            SubjectId = curriculum.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            GradeLevel = curriculum.GradeLevel,
            Title = curriculum.Title,
            Description = curriculum.Description,
            SourceFile = curriculum.SourceFile,
            ReviewStatus = curriculum.ReviewStatus.ToString(),
            ClassIds = classes.Select(id => id.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant()).ToList(),
            NodeIds = nodes.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList(),
            RowVersion = curriculum.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return UpdateCurriculumResult.Success(dto);
    }
}
