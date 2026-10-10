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

        if (!isTeacher)
        {
            return ArchiveCurriculumResult.Failure(ErrorCodes.ForbiddenResource);
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

        var usages = await AcademicDependencyGuards.CurriculumUsageAsync(_dbContext, centerId, curriculumId, cancellationToken);
        if (usages.Count > 0)
            return ArchiveCurriculumResult.Failure(ErrorCodes.InvalidStateTransition, AcademicDependencyGuards.ClassBlockMessage(usages));
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return ArchiveCurriculumResult.Failure(ErrorCodes.ValidationFailed, "Nhập lý do lưu trữ giáo trình (tối đa 500 ký tự).");

        var before = new { curriculum.Title, Status = curriculum.ReviewStatus.ToString(), curriculum.RowVersion };
        curriculum.ReviewStatus = ReviewStatus.Archived;
        var applications = await _dbContext.ClassCurriculumApplications.Where(a => a.CenterId == centerId && a.CurriculumId == curriculumId && a.EndedAt == null).ToListAsync(cancellationToken);
        foreach (var application in applications)
        {
            application.EndedAt = _timeProvider.GetUtcNow().UtcDateTime; application.EndedBy = actorId;
            application.EndReason = request.Reason.Trim();
        }
        curriculum.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        curriculum.UpdatedBy = actorId;
        curriculum.RowVersion++;
        AcademicDependencyGuards.Audit(_dbContext, centerId, actorId, "CurriculumArchived", "Curriculum", curriculumId.ToString("D"),
            before, new { curriculum.Title, Status = curriculum.ReviewStatus.ToString(), curriculum.RowVersion }, curriculum.UpdatedAt, request.Reason.Trim());

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return ArchiveCurriculumResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (AcademicDependencyGuards.IsDatabaseGuard(ex))
        {
            _dbContext.ChangeTracker.Clear();
            return ArchiveCurriculumResult.Failure(ErrorCodes.InvalidStateTransition, AcademicDependencyGuards.ConcurrentDependencyMessage);
        }

        var nodes = await _dbContext.CurriculumNodes
            .AsNoTracking()
            .Where(cn => cn.CurriculumId == curriculumId && cn.CenterId == centerId)
            .OrderBy(cn => cn.OrderIndex)
            .Select(cn => cn.NodeId)
            .ToListAsync(cancellationToken);

        var classes = (await CurriculumClassScopeQuery.ReadAsync(_dbContext, centerId, actorId, curriculumId, cancellationToken))
            .Select(cc => cc.ClassId).ToList();

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

        return ArchiveCurriculumResult.Success(dto);
    }
}
