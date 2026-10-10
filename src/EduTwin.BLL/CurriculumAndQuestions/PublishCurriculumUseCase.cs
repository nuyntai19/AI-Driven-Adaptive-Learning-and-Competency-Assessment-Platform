using System;
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

        if (!isTeacher)
        {
            return PublishCurriculumResult.Failure(ErrorCodes.ForbiddenResource);
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

        var assignedIds = await _dbContext.CurriculumClasses.Where(cc => cc.CenterId == centerId && cc.CurriculumId == curriculumId)
            .Select(cc => cc.ClassId).ToListAsync(cancellationToken);
        var publishCandidates = await _dbContext.Classes.Where(c => c.CenterId == centerId).ToListAsync(cancellationToken);
        var targetClasses = publishCandidates.Where(c => assignedIds.Contains(c.ClassId)).ToList();
        if (targetClasses.Count != assignedIds.Count || targetClasses.Any(c => c.TeacherId != actorId || c.LearningScope != ClassLearningScope.Current ||
            c.IsDeleted || c.Status != ClassStatus.Active || !c.GradeLevel.HasValue || !curriculum.GradeLevel.HasValue || c.GradeLevel != curriculum.GradeLevel))
            return PublishCurriculumResult.Failure(ErrorCodes.InvalidStateTransition);
        foreach (var cls in targetClasses)
            if (await _dbContext.ClassCurriculumApplications.AnyAsync(a => a.CenterId == centerId && a.ClassId == cls.ClassId && a.EndedAt == null && a.ApplicationRole == "Primary", cancellationToken))
                return PublishCurriculumResult.Failure(ErrorCodes.InvalidStateTransition);

        curriculum.ReviewStatus = ReviewStatus.Published;
        curriculum.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        curriculum.UpdatedBy = actorId;
        curriculum.RowVersion++;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            foreach (var cls in targetClasses)
            {
                _dbContext.ClassCurriculumApplications.Add(CurriculumApplicationUseCase.NewApplication(curriculum, cls, actorId, _timeProvider.GetUtcNow().UtcDateTime));
                cls.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime; cls.UpdatedBy = actorId;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken); _dbContext.ChangeTracker.Clear();
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

        return PublishCurriculumResult.Success(dto);
    }
}
