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

public sealed class GetStudentTwinUseCase : IGetStudentTwinUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetStudentTwinUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<StudentTwinResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty)
        {
            return StudentTwinResult.NotFound();
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        if (subjectId == Guid.Empty)
        {
            return StudentTwinResult.ValidationFailed();
        }

        var studentExists = await _dbContext.Students.AsNoTracking()
            .AnyAsync(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted, cancellationToken);
        if (!studentExists)
        {
            return StudentTwinResult.NotFound();
        }

        var subjectExists = await _dbContext.Subjects.AsNoTracking()
            .AnyAsync(s => s.CenterId == centerId && s.SubjectId == subjectId && !s.IsDeleted, cancellationToken);
        if (!subjectExists)
        {
            return StudentTwinResult.NotFound();
        }

        // 1. Active topics for subject + student KnowledgeTwins
        var activeTopics = await _dbContext.KnowledgeNodes.AsNoTracking()
            .Where(n => n.CenterId == centerId && n.SubjectId == subjectId && n.IsActive && !n.IsDeleted)
            .OrderBy(n => n.OrderIndex)
            .Select(n => new { n.NodeId, n.NodeName })
            .ToListAsync(cancellationToken);

        var activeTopicIds = activeTopics.Select(t => t.NodeId).ToList();

        var twins = await _dbContext.KnowledgeTwins.AsNoTracking()
            .Where(kt => kt.CenterId == centerId &&
                         kt.StudentId == studentId &&
                         kt.SubjectId == subjectId &&
                         activeTopicIds.Contains(kt.TopicNodeId) &&
                         !kt.IsDeleted)
            .ToDictionaryAsync(kt => kt.TopicNodeId, cancellationToken);

        var topicDtos = activeTopics.Select(t =>
        {
            twins.TryGetValue(t.NodeId, out var kt);
            return new TopicTwinNodeDto
            {
                TopicNodeId = t.NodeId.ToString(CultureInfo.InvariantCulture),
                TopicName = t.NodeName,
                MasteryPercentage = kt?.MasteryPercentage ?? 0m,
                EvidenceCount = kt?.EvidenceCount ?? 0u,
                LastReasoningQuality = kt?.LastReasoningQuality,
                LastAttemptId = kt?.LastAttemptId?.ToString(CultureInfo.InvariantCulture)
            };
        }).ToList();

        // 2. BehaviorTwin
        var behavior = await _dbContext.BehaviorTwins.AsNoTracking()
            .Where(b => b.CenterId == centerId && b.StudentId == studentId && b.SubjectId == subjectId && !b.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        var behaviorDto = new BehaviorTwinSummaryDto
        {
            AvgTimeSpentSeconds = behavior?.AvgTimeSpentSeconds ?? 0m,
            SkipRate = behavior?.SkipRate ?? 0m,
            ChangeAnswerRate = behavior?.ChangeAnswerRate ?? 0m,
            AvgConfidence = behavior?.AvgConfidence ?? 0m,
            ConfidenceCalibration = behavior?.ConfidenceCalibration ?? 0m,
            AttemptCount = behavior?.AttemptCount ?? 0u
        };

        var resultDto = new StudentTwinDataDto
        {
            StudentId = studentId,
            SubjectId = subjectId,
            Topics = topicDtos,
            Behavior = behaviorDto
        };

        return StudentTwinResult.Success(resultDto);
    }
}
