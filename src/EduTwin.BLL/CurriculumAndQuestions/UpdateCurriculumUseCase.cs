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

        if (!request.GradeLevel.HasValue)
            return UpdateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

        var existingNodes = await _dbContext.CurriculumNodes
            .Where(n => n.CenterId == centerId && n.CurriculumId == curriculumId)
            .OrderBy(n => n.OrderIndex).ToListAsync(cancellationToken);
        var nextNodeIds = existingNodes.Select(n => n.NodeId).ToList();
        if (request.NodeIds is not null && !CurriculumGraphScopeGuard.TryParseNodes(request.NodeIds, out nextNodeIds))
            return UpdateCurriculumResult.Failure(ErrorCodes.ValidationFailed);
        var existingClasses = await _dbContext.CurriculumClasses
            .Where(c => c.CenterId == centerId && c.CurriculumId == curriculumId).ToListAsync(cancellationToken);
        var nextClassIds = existingClasses.Select(c => c.ClassId).ToList();
        if (request.ClassIds is not null)
        {
            nextClassIds = new();
            foreach (var id in request.ClassIds)
            {
                if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty || nextClassIds.Contains(parsed))
                    return UpdateCurriculumResult.Failure(ErrorCodes.ValidationFailed);
                nextClassIds.Add(parsed);
            }
        }
        var validClasses = await _dbContext.Classes.AsNoTracking().Where(c => c.CenterId == centerId &&
            c.TeacherId == actorId && c.SubjectId == curriculum.SubjectId && !c.IsDeleted &&
            c.GradeLevel == request.GradeLevel && c.Status == ClassStatus.Active && c.LearningScope == ClassLearningScope.Current)
            .Select(c => c.ClassId).ToListAsync(cancellationToken);
        var invalidClasses = nextClassIds.Except(validClasses).Select(id => id.ToString("D")).ToList();
        if (invalidClasses.Count > 0)
            return UpdateCurriculumResult.Failure(ErrorCodes.AcademicScopeConflict,
                new CurriculumScopeConflictDto { Message = "Lớp dự kiến phải cùng môn, cùng khối, đang học và do bạn phụ trách.", InvalidClassIds = invalidClasses });

        var replaceComposition = request.NodeIds is not null || request.GradeLevel != curriculum.GradeLevel || request.GraphVersionId.HasValue;
        var graphScope = replaceComposition
            ? await CurriculumGraphScopeGuard.ResolveAsync(_dbContext, centerId, curriculum.SubjectId,
                request.GradeLevel.Value, request.GraphVersionId, curriculum.GraphVersionId, nextNodeIds, true, cancellationToken)
            : new CurriculumGraphScopeResult(curriculum.GraphVersionId, null);
        if (graphScope.Conflict is not null)
            return UpdateCurriculumResult.Failure(ErrorCodes.AcademicScopeConflict, graphScope.Conflict);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Release old node/grade constraints before replacing the pin. Everything is in one transaction.
            if (replaceComposition) _dbContext.CurriculumNodes.RemoveRange(existingNodes);
            if (request.ClassIds is not null) _dbContext.CurriculumClasses.RemoveRange(existingClasses);
            if (replaceComposition || request.ClassIds is not null) await _dbContext.SaveChangesAsync(cancellationToken);

            curriculum.Visibility = visibility;
            curriculum.Title = request.Title;
            curriculum.Description = request.Description;
            curriculum.GradeLevel = request.GradeLevel;
            curriculum.GraphVersionId = graphScope.GraphVersionId;
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            curriculum.UpdatedAt = now;
            curriculum.UpdatedBy = actorId;
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (replaceComposition)
                _dbContext.CurriculumNodes.AddRange(nextNodeIds.Select((id, i) => new CurriculumNode
                { CenterId = centerId, CurriculumId = curriculumId, NodeId = id, OrderIndex = (uint)(i + 1), CreatedAt = now }));
            if (request.ClassIds is not null)
                _dbContext.CurriculumClasses.AddRange(nextClassIds.Select(id => new CurriculumClass
                { CenterId = centerId, CurriculumId = curriculumId, ClassId = id, AssignedAt = now, AssignedBy = actorId }));
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
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
            GraphVersionId = curriculum.GraphVersionId?.ToString("D"),
            GradeLevel = curriculum.GradeLevel,
            Title = curriculum.Title,
            Description = curriculum.Description,
            SourceFile = curriculum.SourceFile,
            ReviewStatus = curriculum.ReviewStatus.ToString(),
            ClassIds = classes.Select(id => id.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant()).ToList(),
            NodeIds = nodes.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList(),
            RowVersion = curriculum.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        dto.ApplicationClasses = (await CurriculumClassScopeQuery.ReadAsync(_dbContext, centerId, actorId, curriculumId, cancellationToken))
            .Select(a => a.ToDto()).ToList();

        return UpdateCurriculumResult.Success(dto);
    }
}
