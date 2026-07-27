using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class DeleteQuestionUseCase : IDeleteQuestionUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public DeleteQuestionUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<DeleteQuestionResult> ExecuteAsync(string questionId, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant and role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return DeleteQuestionResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // Only CenterManager is allowed to delete
        if (!string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal))
        {
            return DeleteQuestionResult.Failure(ErrorCodes.ResourceNotFound); // or ForbiddenResource
        }

        // 2. Parse question ID
        if (string.IsNullOrWhiteSpace(questionId) ||
            !ulong.TryParse(questionId, NumberStyles.None, CultureInfo.InvariantCulture, out var qId) || qId == 0)
            return DeleteQuestionResult.Failure(ErrorCodes.ValidationFailed);

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;

        // 3. Load question
        var question = await _dbContext.Questions
            .FirstOrDefaultAsync(q => q.QuestionId == qId && q.CenterId == centerId, cancellationToken);

        if (question == null)
            return DeleteQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 4. State check - only Draft
        if (question.Status != QuestionStatus.Draft)
            return DeleteQuestionResult.Failure(ErrorCodes.InvalidStateTransition);

        // 5. Check attempts
        var hasAttempts = await _dbContext.Attempts
            .AnyAsync(a => a.QuestionId == qId && a.CenterId == centerId, cancellationToken);
        if (hasAttempts)
            return DeleteQuestionResult.Failure(ErrorCodes.InvalidStateTransition);

        // 6. Delete (Soft delete for MTA)
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        
        question.IsDeleted = true;
        question.DeletedAt = now;
        question.DeletedBy = actorId;

        // Soft delete options
        var options = await _dbContext.QuestionOptions
            .Where(o => o.QuestionId == qId && o.CenterId == centerId && !o.IsDeleted)
            .ToListAsync(cancellationToken);
        
        foreach(var opt in options)
        {
            opt.IsDeleted = true;
            opt.DeletedAt = now;
            opt.DeletedBy = actorId;
        }

        // Hard delete mappings (Since QuestionKnowledgeNode doesn't have IsDeleted flag - wait, let me check the DAL)
        var mappings = await _dbContext.QuestionKnowledgeNodes
            .Where(m => m.QuestionId == qId && m.CenterId == centerId)
            .ToListAsync(cancellationToken);
        
        _dbContext.QuestionKnowledgeNodes.RemoveRange(mappings);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return DeleteQuestionResult.Success();
    }
}
