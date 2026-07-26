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

public class ListQuestionsUseCase : IListQuestionsUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListQuestionsUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<ListQuestionsResult> ExecuteAsync(QuestionListQuery query, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant and role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return ListQuestionsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 2. Pagination validation
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : (query.PageSize > 100 ? 100 : query.PageSize);

        // 3. Query filter validation
        ulong? topicNodeId = null;
        if (!string.IsNullOrWhiteSpace(query.TopicId))
        {
            if (!ulong.TryParse(query.TopicId, NumberStyles.None, CultureInfo.InvariantCulture, out var tid) || tid == 0)
                return ListQuestionsResult.Failure(ErrorCodes.ValidationFailed);
            topicNodeId = tid;
        }

        QuestionType? filterType = null;
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            if (!Enum.TryParse<QuestionType>(query.Type, ignoreCase: false, out var qt) ||
                !string.Equals(query.Type, qt.ToString(), StringComparison.Ordinal))
                return ListQuestionsResult.Failure(ErrorCodes.ValidationFailed);
            filterType = qt;
        }

        QuestionStatus? filterStatus = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<QuestionStatus>(query.Status, ignoreCase: false, out var qs) ||
                !string.Equals(query.Status, qs.ToString(), StringComparison.Ordinal))
                return ListQuestionsResult.Failure(ErrorCodes.ValidationFailed);
            filterStatus = qs;
        }

        if (query.Difficulty.HasValue && (query.Difficulty.Value < 1 || query.Difficulty.Value > 5))
            return ListQuestionsResult.Failure(ErrorCodes.ValidationFailed);

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 4. Build base query (Global query filter already applies CenterId + !IsDeleted)
        var baseQ = _dbContext.Questions
            .AsNoTracking()
            .Where(q => q.CenterId == centerId);

        if (isTeacher)
            baseQ = baseQ.Where(q => q.CreatedByTeacherId == actorId);

        if (query.SubjectId.HasValue && query.SubjectId.Value != Guid.Empty)
            baseQ = baseQ.Where(q => q.SubjectId == query.SubjectId.Value);

        if (topicNodeId.HasValue)
            baseQ = baseQ.Where(q => q.PrimaryTopicNodeId == topicNodeId.Value);

        if (filterType.HasValue)
            baseQ = baseQ.Where(q => q.QuestionType == filterType.Value);

        if (query.Difficulty.HasValue)
            baseQ = baseQ.Where(q => q.Difficulty == query.Difficulty.Value);

        if (filterStatus.HasValue)
            baseQ = baseQ.Where(q => q.Status == filterStatus.Value);

        var totalItems = await baseQ.LongCountAsync(cancellationToken);

        var questions = await baseQ
            .OrderByDescending(q => q.UpdatedAt)
            .ThenBy(q => q.QuestionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
            return ListQuestionsResult.Success(new List<QuestionDto>(), totalItems);

        var questionIds = questions.Select(q => q.QuestionId).ToList();

        var options = await _dbContext.QuestionOptions
            .AsNoTracking()
            .Where(o => o.CenterId == centerId && questionIds.Contains(o.QuestionId) && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        var mappings = await _dbContext.QuestionKnowledgeNodes
            .AsNoTracking()
            .Where(m => m.CenterId == centerId && questionIds.Contains(m.QuestionId))
            .ToListAsync(cancellationToken);

        var optionsMap = options.GroupBy(o => o.QuestionId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());
        var mappingsMap = mappings.GroupBy(m => m.QuestionId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var dtos = questions.Select(q => QuestionProjection.ToDto(
            q,
            optionsMap.TryGetValue(q.QuestionId, out var opts) ? opts : Enumerable.Empty<EduTwin.DAL.CurriculumAndQuestions.QuestionOption>(),
            mappingsMap.TryGetValue(q.QuestionId, out var maps) ? maps : Enumerable.Empty<EduTwin.DAL.CurriculumAndQuestions.QuestionKnowledgeNode>()
        )).ToList();

        return ListQuestionsResult.Success(dtos, totalItems);
    }
}
