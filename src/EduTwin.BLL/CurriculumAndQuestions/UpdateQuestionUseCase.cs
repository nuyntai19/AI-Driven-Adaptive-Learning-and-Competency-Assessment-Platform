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

public class UpdateQuestionUseCase : IUpdateQuestionUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public UpdateQuestionUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateQuestionResult> ExecuteAsync(string questionId, UpdateQuestionRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant and role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return UpdateQuestionResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 2. Parse question ID
        if (string.IsNullOrWhiteSpace(questionId) ||
            !ulong.TryParse(questionId, NumberStyles.None, CultureInfo.InvariantCulture, out var qId) || qId == 0)
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        // 3. RowVersion validation
        if (string.IsNullOrEmpty(request.RowVersion))
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        foreach (var ch in request.RowVersion)
            if (ch < '0' || ch > '9') return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (!ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var rowVersion) || rowVersion == 0)
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 4. Request field validation
        if (string.IsNullOrWhiteSpace(request.PrimaryTopicNodeId))
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        foreach (var ch in request.PrimaryTopicNodeId)
            if (ch < '0' || ch > '9') return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (!ulong.TryParse(request.PrimaryTopicNodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var topicNodeId) || topicNodeId == 0)
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (!Enum.TryParse<QuestionType>(request.QuestionType, ignoreCase: false, out var questionType) ||
            !string.Equals(request.QuestionType, questionType.ToString(), StringComparison.Ordinal))
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (request.Difficulty < 1 || request.Difficulty > 5)
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.QuestionText))
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.CorrectAnswer))
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.Solution))
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (request.MaxScore <= 0)
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (request.EstimatedTimeSeconds == 0)
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.LanguageCode) ||
            (!string.Equals(request.LanguageCode, "vi", StringComparison.Ordinal) &&
             !string.Equals(request.LanguageCode, "en", StringComparison.Ordinal)))
            return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        // 5. Load question
        var question = await _dbContext.Questions
            .FirstOrDefaultAsync(q => q.QuestionId == qId && q.CenterId == centerId, cancellationToken);

        if (question == null)
            return UpdateQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 6. Ownership check
        if (isTeacher && question.CreatedByTeacherId != actorId)
            return UpdateQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 7. State check — only Draft can be updated
        if (question.Status != QuestionStatus.Draft)
            return UpdateQuestionResult.Failure(ErrorCodes.InvalidStateTransition);

        // 8. Concurrency
        if (question.RowVersion != rowVersion)
            return UpdateQuestionResult.Failure(ErrorCodes.ConcurrencyConflict);

        // 9. Validate new PrimaryTopicNode — must belong to same subject
        var topicNode = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NodeId == topicNodeId && n.CenterId == centerId && !n.IsDeleted && n.IsActive && n.SubjectId == question.SubjectId, cancellationToken);

        if (topicNode == null)
            return UpdateQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 10. MultipleChoice option validation
        if (questionType == QuestionType.MultipleChoice)
        {
            if (request.Options == null || request.Options.Count < 2)
                return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);
            if (request.Options.Count(o => o.IsCorrect) != 1)
                return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);
        }

        // AnswerEvaluationMode validation & matrix enforcement
        QuestionAnswerEvaluationMode evalMode;
        if (string.IsNullOrWhiteSpace(request.AnswerEvaluationMode))
        {
            evalMode = questionType switch
            {
                QuestionType.MultipleChoice => QuestionAnswerEvaluationMode.TextExact,
                QuestionType.Essay => QuestionAnswerEvaluationMode.Manual,
                _ => question.AnswerEvaluationMode
            };
        }
        else
        {
            if (!Enum.TryParse<QuestionAnswerEvaluationMode>(request.AnswerEvaluationMode, ignoreCase: false, out evalMode)
                || !Enum.IsDefined(evalMode)
                || request.AnswerEvaluationMode != evalMode.ToString())
            {
                return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);
            }

            if (questionType == QuestionType.MultipleChoice && evalMode != QuestionAnswerEvaluationMode.TextExact)
                return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);

            if (questionType == QuestionType.Essay && evalMode != QuestionAnswerEvaluationMode.Manual)
                return UpdateQuestionResult.Failure(ErrorCodes.ValidationFailed);
        }

        // 11. Update scalar fields
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        question.PrimaryTopicNodeId = topicNodeId;
        question.QuestionType = questionType;
        question.AnswerEvaluationMode = evalMode;
        question.Difficulty = request.Difficulty;
        question.QuestionText = request.QuestionText;
        question.CorrectAnswer = request.CorrectAnswer;
        question.Solution = request.Solution;
        question.ExpectedReasoning = request.ExpectedReasoning;
        question.GradingCriteria = request.GradingCriteria ?? new Contracts.CurriculumAndQuestions.GradingCriteria();
        question.MaxScore = request.MaxScore;
        question.EstimatedTimeSeconds = request.EstimatedTimeSeconds;
        question.ReasoningRequired = request.ReasoningRequired;
        question.LanguageCode = request.LanguageCode;
        question.UpdatedAt = now;
        question.UpdatedBy = actorId;

        // 12. Replace options atomically if provided
        var existingOptions = await _dbContext.QuestionOptions
            .Where(o => o.QuestionId == qId && o.CenterId == centerId)
            .ToListAsync(cancellationToken);

        foreach (var opt in existingOptions.Where(o => !o.IsDeleted))
        {
            opt.IsDeleted = true;
            opt.DeletedAt = now;
            opt.DeletedBy = actorId;
            opt.OrderIndex = 9999 + opt.OrderIndex; // temporarily move out of the way to avoid constraint conflicts
        }

        var newOptions = new List<QuestionOption>();
        if (request.Options != null)
        {
            foreach (var optInput in request.Options)
            {
                var opt = existingOptions.FirstOrDefault(o => o.OptionLabel == optInput.OptionLabel);
                if (opt != null)
                {
                    opt.IsDeleted = false;
                    opt.DeletedAt = null;
                    opt.DeletedBy = null;
                    opt.OptionText = optInput.OptionText;
                    opt.IsCorrect = optInput.IsCorrect;
                    opt.OrderIndex = optInput.OrderIndex;
                    opt.UpdatedAt = now;
                    opt.UpdatedBy = actorId;
                    newOptions.Add(opt);
                }
                else
                {
                    var optId = question.QuestionId * 1000 + (ulong)(existingOptions.Count + newOptions.Count + 100);
                    opt = new QuestionOption
                    {
                        OptionId = optId,
                        CenterId = centerId,
                        QuestionId = question.QuestionId,
                        OptionLabel = optInput.OptionLabel,
                        OptionText = optInput.OptionText,
                        IsCorrect = optInput.IsCorrect,
                        OrderIndex = optInput.OrderIndex,
                        CreatedAt = now,
                        CreatedBy = actorId,
                        UpdatedAt = now,
                        UpdatedBy = actorId
                    };
                    _dbContext.QuestionOptions.Add(opt);
                    newOptions.Add(opt);
                }
            }
        }

        // 13. Replace knowledge mappings atomically
        var existingMappings = await _dbContext.QuestionKnowledgeNodes
            .Where(m => m.QuestionId == qId && m.CenterId == centerId)
            .ToListAsync(cancellationToken);

        _dbContext.QuestionKnowledgeNodes.RemoveRange(existingMappings);

        var newMappings = new List<QuestionKnowledgeNode>();
        if (request.KnowledgeMappings != null)
        {
            foreach (var km in request.KnowledgeMappings)
            {
                if (!ulong.TryParse(km.NodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var kmNodeId)) continue;
                if (!Enum.TryParse<MappingRole>(km.MappingRole, ignoreCase: false, out var mr)) continue;
                var mapping = new QuestionKnowledgeNode
                {
                    CenterId = centerId,
                    QuestionId = question.QuestionId,
                    NodeId = kmNodeId,
                    MappingRole = mr,
                    CreatedAt = now
                };
                _dbContext.QuestionKnowledgeNodes.Add(mapping);
                newMappings.Add(mapping);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return UpdateQuestionResult.Success(QuestionProjection.ToDto(question, newOptions, newMappings));
    }
}
