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
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class CreateQuestionUseCase : ICreateQuestionUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly ICoordinateAnswerNormalizer _coordinateNormalizer;

    public CreateQuestionUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        ICoordinateAnswerNormalizer? coordinateNormalizer = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _coordinateNormalizer = coordinateNormalizer ?? new CoordinateAnswerNormalizer();
    }

    public async Task<CreateQuestionResult> ExecuteAsync(CreateQuestionRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant and role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return CreateQuestionResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 2. Request validation
        if (request.SubjectId == Guid.Empty)
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.PrimaryTopicNodeId))
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        foreach (var ch in request.PrimaryTopicNodeId)
            if (ch < '0' || ch > '9') return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (!ulong.TryParse(request.PrimaryTopicNodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var topicNodeId) || topicNodeId == 0)
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (!Enum.TryParse<QuestionType>(request.QuestionType, ignoreCase: false, out var questionType) ||
            !Enum.IsDefined(typeof(QuestionType), questionType) ||
            !string.Equals(request.QuestionType, questionType.ToString(), StringComparison.Ordinal))
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (request.Difficulty < 1 || request.Difficulty > 5)
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.QuestionText))
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.CorrectAnswer))
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.Solution))
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (request.MaxScore <= 0)
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (request.EstimatedTimeSeconds == 0)
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.LanguageCode) ||
            (!string.Equals(request.LanguageCode, "vi", StringComparison.Ordinal) &&
             !string.Equals(request.LanguageCode, "en", StringComparison.Ordinal)))
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

        // MultipleChoice option validation (warn on create: require at least structure if provided)
        if (questionType == QuestionType.MultipleChoice)
        {
            if (request.Options == null || request.Options.Count < 2)
                return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

            var correctCount = request.Options.Count(o => o.IsCorrect);
            if (correctCount != 1)
                return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);
        }

        // AnswerEvaluationMode validation & matrix enforcement
        QuestionAnswerEvaluationMode evalMode;
        if (string.IsNullOrWhiteSpace(request.AnswerEvaluationMode))
        {
            evalMode = questionType switch
            {
                QuestionType.MultipleChoice => QuestionAnswerEvaluationMode.TextExact,
                QuestionType.Essay => QuestionAnswerEvaluationMode.Manual,
                _ => QuestionAnswerEvaluationMode.TextExact
            };
        }
        else
        {
            if (!Enum.TryParse<QuestionAnswerEvaluationMode>(request.AnswerEvaluationMode, ignoreCase: false, out evalMode)
                || !Enum.IsDefined(evalMode)
                || request.AnswerEvaluationMode != evalMode.ToString())
            {
                return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);
            }

            if (questionType == QuestionType.MultipleChoice && evalMode != QuestionAnswerEvaluationMode.TextExact)
                return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

            if (questionType == QuestionType.Essay && evalMode != QuestionAnswerEvaluationMode.Manual)
                return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (evalMode == QuestionAnswerEvaluationMode.Coordinate2D
            && (questionType != QuestionType.ShortAnswer
                || !_coordinateNormalizer.TryNormalize(request.CorrectAnswer, out _)))
        {
            return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);
        }

        // Knowledge mapping validation
        if (request.KnowledgeMappings != null)
        {
            foreach (var km in request.KnowledgeMappings)
            {
                if (string.IsNullOrWhiteSpace(km.NodeId))
                    return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);
                if (!ulong.TryParse(km.NodeId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);
                if (!Enum.TryParse<MappingRole>(km.MappingRole, ignoreCase: false, out var mr) ||
                    !string.Equals(km.MappingRole, mr.ToString(), StringComparison.Ordinal))
                    return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);
            }
        }

        // 3. Actor validation
        Guid effectiveTeacherId = actorId;
        if (isTeacher)
        {
            if (request.TeacherId != null)
                return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);

            var teacher = await _dbContext.Teachers
                .AsNoTracking()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == actorId && t.CenterId == centerId && !t.IsDeleted, cancellationToken);

            if (teacher == null || teacher.User == null || teacher.User.IsDeleted ||
                teacher.User.CenterId != centerId || teacher.User.RoleName != UserRole.Teacher ||
                teacher.User.Status != UserStatus.Active)
                return CreateQuestionResult.Failure(ErrorCodes.ResourceNotFound);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.TeacherId) ||
                !Guid.TryParse(request.TeacherId, out effectiveTeacherId) ||
                effectiveTeacherId == Guid.Empty)
            {
                return CreateQuestionResult.Failure(ErrorCodes.ValidationFailed);
            }

            var selectedTeacher = await _dbContext.Teachers
                .AsNoTracking()
                .Include(t => t.User)
                .FirstOrDefaultAsync(
                    t => t.TeacherId == effectiveTeacherId && t.CenterId == centerId && !t.IsDeleted,
                    cancellationToken);

            if (selectedTeacher == null || selectedTeacher.User == null ||
                selectedTeacher.User.CenterId != centerId ||
                selectedTeacher.User.IsDeleted ||
                selectedTeacher.User.RoleName != UserRole.Teacher ||
                selectedTeacher.User.Status != UserStatus.Active)
            {
                return CreateQuestionResult.Failure(ErrorCodes.ResourceNotFound);
            }
        }

        // 4. Subject validation
        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId && s.CenterId == centerId && s.IsActive && !s.IsDeleted, cancellationToken);

        if (subject == null)
            return CreateQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 5. PrimaryTopicNode validation — must belong to same subject
        var topicNode = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NodeId == topicNodeId && n.CenterId == centerId && !n.IsDeleted && n.IsActive && n.SubjectId == request.SubjectId, cancellationToken);

        if (topicNode == null)
            return CreateQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 6. Validate knowledge mapping nodes exist in same subject
        var mappingNodeIds = new List<ulong>();
        if (request.KnowledgeMappings != null && request.KnowledgeMappings.Count > 0)
        {
            foreach (var km in request.KnowledgeMappings)
            {
                ulong.TryParse(km.NodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var mnId);
                mappingNodeIds.Add(mnId);
            }

            var validNodes = await _dbContext.KnowledgeNodes
                .AsNoTracking()
                .Where(n => n.CenterId == centerId && !n.IsDeleted && n.IsActive &&
                            n.SubjectId == request.SubjectId && mappingNodeIds.Contains(n.NodeId))
                .CountAsync(cancellationToken);

            if (validNodes != mappingNodeIds.Count)
                return CreateQuestionResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 7. Persist
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var question = new Question
        {
            CenterId = centerId,
            SubjectId = request.SubjectId,
            PrimaryTopicNodeId = topicNodeId,
            CreatedByTeacherId = effectiveTeacherId,
            QuestionType = questionType,
            Difficulty = request.Difficulty,
            QuestionText = request.QuestionText,
            CorrectAnswer = request.CorrectAnswer,
            Solution = request.Solution,
            ExpectedReasoning = request.ExpectedReasoning,
            GradingCriteria = request.GradingCriteria ?? new Contracts.CurriculumAndQuestions.GradingCriteria(),
            MaxScore = request.MaxScore,
            EstimatedTimeSeconds = request.EstimatedTimeSeconds,
            ReasoningRequired = request.ReasoningRequired,
            LanguageCode = request.LanguageCode,
            Status = QuestionStatus.Draft,
            AnswerEvaluationMode = evalMode,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId
        };

        var savedOptions = new List<QuestionOption>();
        var savedMappings = new List<QuestionKnowledgeNode>();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Questions.Add(question);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (request.Options != null)
            {
                foreach (var optInput in request.Options)
                {
                    var opt = new QuestionOption
                    {
                        OptionId = question.QuestionId * 1000 + (ulong)savedOptions.Count + 1,
                        CenterId = centerId,
                        QuestionId = question.QuestionId,
                        OptionLabel = optInput.OptionLabel,
                        OptionText = optInput.OptionText,
                        IsCorrect = optInput.IsCorrect,
                        OrderIndex = optInput.OrderIndex,
                        Misconception = optInput.Misconception,
                        CreatedAt = now,
                        CreatedBy = actorId,
                        UpdatedAt = now,
                        UpdatedBy = actorId
                    };
                    _dbContext.QuestionOptions.Add(opt);
                    savedOptions.Add(opt);
                }
            }

            if (request.KnowledgeMappings != null)
            {
                foreach (var km in request.KnowledgeMappings)
                {
                    ulong.TryParse(km.NodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var kmNodeId);
                    Enum.TryParse<MappingRole>(km.MappingRole, out var mr);
                    var mapping = new QuestionKnowledgeNode
                    {
                        CenterId = centerId,
                        QuestionId = question.QuestionId,
                        NodeId = kmNodeId,
                        MappingRole = mr,
                        CreatedAt = now
                    };
                    _dbContext.QuestionKnowledgeNodes.Add(mapping);
                    savedMappings.Add(mapping);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var dto = QuestionProjection.ToDto(question, savedOptions, savedMappings);
        return CreateQuestionResult.Success(dto);
    }
}
