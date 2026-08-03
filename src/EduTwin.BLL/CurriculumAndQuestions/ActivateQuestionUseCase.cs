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

public class ActivateQuestionUseCase : IActivateQuestionUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public ActivateQuestionUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<ActivateQuestionResult> ExecuteAsync(string questionId, ActivateQuestionRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant and role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return ActivateQuestionResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 2. Parse question ID
        if (string.IsNullOrWhiteSpace(questionId) ||
            !ulong.TryParse(questionId, NumberStyles.None, CultureInfo.InvariantCulture, out var qId) || qId == 0)
            return ActivateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        // 3. RowVersion validation
        if (string.IsNullOrEmpty(request.RowVersion))
            return ActivateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        foreach (var ch in request.RowVersion)
            if (ch < '0' || ch > '9') return ActivateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (!ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var rowVersion) || rowVersion == 0)
            return ActivateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 4. Load question
        var question = await _dbContext.Questions
            .FirstOrDefaultAsync(q => q.QuestionId == qId && q.CenterId == centerId, cancellationToken);

        if (question == null)
            return ActivateQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 5. Ownership
        if (isTeacher && question.CreatedByTeacherId != actorId)
            return ActivateQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 6. State check — Draft or Archived can be activated
        if (question.Status != QuestionStatus.Draft && question.Status != QuestionStatus.Archived)
            return ActivateQuestionResult.Failure(ErrorCodes.InvalidStateTransition);

        // 7. Concurrency
        if (question.RowVersion != rowVersion)
            return ActivateQuestionResult.Failure(ErrorCodes.ConcurrencyConflict);

        // 8. MultipleChoice activation guard
        if (question.QuestionType == QuestionType.MultipleChoice)
        {
            var activeOptions = await _dbContext.QuestionOptions
                .AsNoTracking()
                .Where(o => o.QuestionId == qId && o.CenterId == centerId && !o.IsDeleted)
                .ToListAsync(cancellationToken);

            if (activeOptions.Count < 2)
                return ActivateQuestionResult.Failure(ErrorCodes.ValidationFailed);

            if (activeOptions.Count(o => o.IsCorrect) != 1)
                return ActivateQuestionResult.Failure(ErrorCodes.ValidationFailed);
        }

        // 9. Activate
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        question.Status = QuestionStatus.Active;
        question.UpdatedAt = now;
        question.UpdatedBy = actorId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var options = await _dbContext.QuestionOptions
            .AsNoTracking()
            .Where(o => o.QuestionId == qId && o.CenterId == centerId && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        var mappings = await _dbContext.QuestionKnowledgeNodes
            .AsNoTracking()
            .Where(m => m.QuestionId == qId && m.CenterId == centerId)
            .ToListAsync(cancellationToken);

        return ActivateQuestionResult.Success(QuestionProjection.ToDto(question, options, mappings));
    }
}
