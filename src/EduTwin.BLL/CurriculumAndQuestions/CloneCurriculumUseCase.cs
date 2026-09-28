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

public class CloneCurriculumUseCase : ICloneCurriculumUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CloneCurriculumUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<CloneCurriculumResult> ExecuteAsync(Guid curriculumId, CloneCurriculumRequest request, CancellationToken cancellationToken = default)
    {
        if (!CurriculumGuards.TryResolveActor(_tenantContext, out var centerId, out var actorId, out var isTeacher))
        {
            return CloneCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var sourceCurriculum = await _dbContext.Curriculums
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CurriculumId == curriculumId &&
                                      c.CenterId == centerId &&
                                      !c.IsDeleted, cancellationToken);

        if (sourceCurriculum == null)
        {
            return CloneCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!CurriculumGuards.CanAccess(sourceCurriculum, actorId, isTeacher))
        {
            return CloneCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        string newTitle;
        if (!string.IsNullOrWhiteSpace(request?.Title))
        {
            newTitle = request.Title.Trim();
        }
        else
        {
            var baseTitle = sourceCurriculum.Title;
            var suffix = " (Bản sao)";
            if (baseTitle.Length + suffix.Length > 250)
            {
                baseTitle = baseTitle.Substring(0, 250 - suffix.Length);
            }
            newTitle = $"{baseTitle}{suffix}";
        }

        if (newTitle.Length > 250)
        {
            return CloneCurriculumResult.Failure(ErrorCodes.ValidationFailed);
        }

        var sourceNodeIds = await _dbContext.CurriculumNodes
            .AsNoTracking()
            .Where(cn => cn.CurriculumId == curriculumId && cn.CenterId == centerId)
            .OrderBy(cn => cn.OrderIndex)
            .Select(cn => cn.NodeId)
            .ToListAsync(cancellationToken);

        var activeNodeIds = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.CenterId == centerId &&
                        n.SubjectId == sourceCurriculum.SubjectId &&
                        n.IsActive &&
                        !n.IsDeleted &&
                        sourceNodeIds.Contains(n.NodeId))
            .Select(n => n.NodeId)
            .ToListAsync(cancellationToken);

        var activeNodeIdSet = new HashSet<ulong>(activeNodeIds);
        var filteredOrderedNodeIds = sourceNodeIds.Where(id => activeNodeIdSet.Contains(id)).ToList();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var newCurriculumId = Guid.NewGuid();

        var newCurriculum = new Curriculum
        {
            CurriculumId = newCurriculumId,
            CenterId = centerId,
            TeacherId = isTeacher ? actorId : sourceCurriculum.TeacherId,
            SubjectId = sourceCurriculum.SubjectId,
            Title = newTitle,
            Description = sourceCurriculum.Description,
            SourceFile = null,
            ReviewStatus = ReviewStatus.Draft,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var newCurriculumNodes = new List<CurriculumNode>();
        for (int i = 0; i < filteredOrderedNodeIds.Count; i++)
        {
            newCurriculumNodes.Add(new CurriculumNode
            {
                CenterId = centerId,
                CurriculumId = newCurriculumId,
                NodeId = filteredOrderedNodeIds[i],
                OrderIndex = (uint)(i + 1),
                CreatedAt = now
            });
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Curriculums.Add(newCurriculum);
            if (newCurriculumNodes.Count > 0)
            {
                _dbContext.CurriculumNodes.AddRange(newCurriculumNodes);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var dto = new CurriculumDto
        {
            CurriculumId = newCurriculum.CurriculumId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            TeacherId = newCurriculum.TeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            SubjectId = newCurriculum.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = newCurriculum.Title,
            Description = newCurriculum.Description,
            SourceFile = null,
            ReviewStatus = ReviewStatus.Draft.ToString(),
            ClassIds = new List<string>(),
            NodeIds = newCurriculumNodes.OrderBy(cn => cn.OrderIndex).Select(cn => cn.NodeId.ToString(CultureInfo.InvariantCulture)).ToList(),
            RowVersion = newCurriculum.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return CloneCurriculumResult.Success(dto);
    }
}
