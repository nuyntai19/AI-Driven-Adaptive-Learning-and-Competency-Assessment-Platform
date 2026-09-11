using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin;

public sealed class GetStudentTwinHistoryUseCase : IGetStudentTwinHistoryUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetStudentTwinHistoryUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<TwinHistoryResult> ExecuteAsync(
        Guid subjectId,
        ulong? topicId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty)
        {
            return TwinHistoryResult.NotFound();
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        if (subjectId == Guid.Empty)
        {
            return TwinHistoryResult.ValidationFailed();
        }

        var studentExists = await _dbContext.Students.AsNoTracking()
            .AnyAsync(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted, cancellationToken);
        if (!studentExists)
        {
            return TwinHistoryResult.NotFound();
        }

        var subjectExists = await _dbContext.Subjects.AsNoTracking()
            .AnyAsync(s => s.CenterId == centerId && s.SubjectId == subjectId && !s.IsDeleted, cancellationToken);
        if (!subjectExists)
        {
            return TwinHistoryResult.NotFound();
        }

        var query = _dbContext.TwinUpdateHistories.AsNoTracking()
            .Where(h => h.CenterId == centerId &&
                        h.StudentId == studentId &&
                        h.SubjectId == subjectId);

        if (topicId.HasValue)
        {
            query = query.Where(h => h.TopicNodeId == topicId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(h => h.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(h => h.CreatedAt <= to.Value);
        }

        var items = await query
            .OrderByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.HistoryId)
            .Select(h => new TwinHistoryItemDto
            {
                HistoryId = h.HistoryId.ToString(CultureInfo.InvariantCulture),
                TopicNodeId = h.TopicNodeId.ToString(CultureInfo.InvariantCulture),
                TopicName = h.TopicNode.NodeName,
                EventSource = h.EventSource.ToString(),
                PreviousMastery = h.PreviousMastery,
                NewMastery = h.NewMastery,
                Delta = h.MasteryDelta,
                ReasoningQuality = h.EffectiveReasoningQuality,
                Explanation = h.Explanation,
                RecordedAt = h.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return TwinHistoryResult.Success(items);
    }
}
