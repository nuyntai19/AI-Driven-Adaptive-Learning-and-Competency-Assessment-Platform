using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class GetQuestionUseCase : IGetQuestionUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetQuestionUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<GetQuestionResult> ExecuteAsync(string questionId, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant and role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return GetQuestionResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 2. Parse ID
        if (string.IsNullOrWhiteSpace(questionId) ||
            !ulong.TryParse(questionId, NumberStyles.None, CultureInfo.InvariantCulture, out var qId) || qId == 0)
            return GetQuestionResult.Failure(ErrorCodes.ValidationFailed);

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 3. Load question (Global Query Filter applies: CenterId + !IsDeleted)
        var question = await _dbContext.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.QuestionId == qId && q.CenterId == centerId, cancellationToken);

        if (question == null)
            return GetQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 4. Teacher ownership check
        if (isTeacher && question.CreatedByTeacherId != actorId)
            return GetQuestionResult.Failure(ErrorCodes.ResourceNotFound);

        // 5. Load options and mappings
        var options = await _dbContext.QuestionOptions
            .AsNoTracking()
            .Where(o => o.QuestionId == qId && o.CenterId == centerId && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        var mappings = await _dbContext.QuestionKnowledgeNodes
            .AsNoTracking()
            .Where(m => m.QuestionId == qId && m.CenterId == centerId)
            .ToListAsync(cancellationToken);

        return GetQuestionResult.Success(QuestionProjection.ToDto(question, options, mappings));
    }
}
