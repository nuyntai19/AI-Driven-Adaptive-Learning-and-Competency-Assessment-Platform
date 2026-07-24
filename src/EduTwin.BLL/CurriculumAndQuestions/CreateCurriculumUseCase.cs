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
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class CreateCurriculumUseCase : ICreateCurriculumUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateCurriculumUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<CreateCurriculumResult> ExecuteAsync(CreateCurriculumRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant and role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return CreateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 2. Request Validation
        if (request.SubjectId == Guid.Empty)
            return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.Title))
            return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

        if (request.Title.Length > 250)
            return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

        if (request.NodeIds == null)
            return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

        var parsedNodeIds = new List<ulong>();
        var seenParsedNodeIds = new HashSet<ulong>();

        foreach (var nodeIdStr in request.NodeIds)
        {
            if (string.IsNullOrEmpty(nodeIdStr))
                return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

            foreach (var ch in nodeIdStr)
            {
                if (ch < '0' || ch > '9')
                    return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);
            }

            if (!ulong.TryParse(nodeIdStr, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedNodeId) || parsedNodeId == 0)
                return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

            if (!seenParsedNodeIds.Add(parsedNodeId))
                return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

            parsedNodeIds.Add(parsedNodeId);
        }

        // 3. Owner Resolution
        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);
        Guid ownerTeacherId;

        if (isTeacher)
        {
            if (request.TeacherId != null)
                return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

            ownerTeacherId = actorId;

            var teacherEntity = await _dbContext.Teachers
                .AsNoTracking()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == ownerTeacherId && t.CenterId == centerId && !t.IsDeleted, cancellationToken);

            if (teacherEntity == null ||
                teacherEntity.User == null ||
                teacherEntity.User.CenterId != centerId ||
                teacherEntity.User.IsDeleted ||
                teacherEntity.User.RoleName != UserRole.Teacher ||
                teacherEntity.User.Status != UserStatus.Active)
            {
                return CreateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.TeacherId) || !Guid.TryParse(request.TeacherId, out var targetTeacherId) || targetTeacherId == Guid.Empty)
                return CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed);

            var teacherEntity = await _dbContext.Teachers
                .AsNoTracking()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == targetTeacherId && t.CenterId == centerId && !t.IsDeleted, cancellationToken);

            if (teacherEntity == null ||
                teacherEntity.User == null ||
                teacherEntity.User.CenterId != centerId ||
                teacherEntity.User.IsDeleted ||
                teacherEntity.User.RoleName != UserRole.Teacher ||
                teacherEntity.User.Status != UserStatus.Active)
            {
                return CreateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
            }

            ownerTeacherId = targetTeacherId;
        }

        // 4. Reference Validation
        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId && !c.IsDeleted && c.Status == CenterStatus.Active, cancellationToken);

        if (center == null)
            return CreateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId && s.CenterId == centerId && s.IsActive && !s.IsDeleted, cancellationToken);

        if (subject == null)
            return CreateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);

        if (parsedNodeIds.Count > 0)
        {
            var distinctNodeIds = parsedNodeIds.Distinct().ToList();
            var dbNodes = await _dbContext.KnowledgeNodes
                .AsNoTracking()
                .Where(n => n.CenterId == centerId &&
                            n.SubjectId == request.SubjectId &&
                            n.IsActive &&
                            !n.IsDeleted &&
                            distinctNodeIds.Contains(n.NodeId))
                .ToListAsync(cancellationToken);

            if (dbNodes.Count != distinctNodeIds.Count)
                return CreateCurriculumResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 5. Atomic Persistence
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var curriculumId = Guid.NewGuid();

        var curriculum = new Curriculum
        {
            CurriculumId = curriculumId,
            CenterId = centerId,
            TeacherId = ownerTeacherId,
            SubjectId = request.SubjectId,
            Title = request.Title,
            Description = request.Description,
            SourceFile = null,
            ReviewStatus = ReviewStatus.Draft,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var curriculumNodes = new List<CurriculumNode>();
        for (int i = 0; i < parsedNodeIds.Count; i++)
        {
            curriculumNodes.Add(new CurriculumNode
            {
                CenterId = centerId,
                CurriculumId = curriculumId,
                NodeId = parsedNodeIds[i],
                OrderIndex = (uint)(i + 1),
                CreatedAt = now
            });
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Curriculums.Add(curriculum);
            if (curriculumNodes.Count > 0)
            {
                _dbContext.CurriculumNodes.AddRange(curriculumNodes);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // 6. Build Response DTO
        var dto = new CurriculumDto
        {
            CurriculumId = curriculum.CurriculumId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            TeacherId = curriculum.TeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            SubjectId = curriculum.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = curriculum.Title,
            Description = curriculum.Description,
            SourceFile = null,
            ReviewStatus = ReviewStatus.Draft.ToString(),
            ClassIds = new List<string>(),
            NodeIds = curriculumNodes.OrderBy(cn => cn.OrderIndex).Select(cn => cn.NodeId.ToString(CultureInfo.InvariantCulture)).ToList(),
            RowVersion = curriculum.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return CreateCurriculumResult.Success(dto);
    }
}
