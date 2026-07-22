using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.KnowledgeGraph;

public class DeleteSubjectUseCase : IDeleteSubjectUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public DeleteSubjectUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<DeleteSubjectResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal) ||
            subjectId == Guid.Empty)
        {
            return DeleteSubjectResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
        {
            return DeleteSubjectResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var subject = await _dbContext.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId && s.CenterId == centerId && !s.IsDeleted, cancellationToken);

        if (subject == null)
        {
            return DeleteSubjectResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (await _dbContext.Classes.AnyAsync(c => c.CenterId == centerId && c.SubjectId == subjectId && !c.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.Curriculums.AnyAsync(c => c.CenterId == centerId && c.SubjectId == subjectId && !c.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.Questions.AnyAsync(q => q.CenterId == centerId && q.SubjectId == subjectId && !q.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.KnowledgeNodes.AnyAsync(n => n.CenterId == centerId && n.SubjectId == subjectId && !n.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.KnowledgeEdges.AnyAsync(e => e.CenterId == centerId && e.SubjectId == subjectId && !e.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.StudentSubjectGoals.AnyAsync(g => g.CenterId == centerId && g.SubjectId == subjectId && !g.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.KnowledgeTwins.AnyAsync(k => k.CenterId == centerId && k.SubjectId == subjectId && !k.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.BehaviorTwins.AnyAsync(b => b.CenterId == centerId && b.SubjectId == subjectId && !b.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.TwinUpdateHistories.AnyAsync(h => h.CenterId == centerId && h.SubjectId == subjectId, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.LearningPaths.AnyAsync(l => l.CenterId == centerId && l.SubjectId == subjectId && !l.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        if (await _dbContext.Recommendations.AnyAsync(r => r.CenterId == centerId && r.SubjectId == subjectId && !r.IsDeleted, cancellationToken))
            return DeleteSubjectResult.Failure(ErrorCodes.InvalidStateTransition);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var managerId = _tenantContext.UserId.Value;

        subject.IsDeleted = true;
        subject.DeletedAt = now;
        subject.DeletedBy = managerId;
        subject.UpdatedAt = now;
        subject.UpdatedBy = managerId;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return DeleteSubjectResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        return DeleteSubjectResult.Success();
    }
}
