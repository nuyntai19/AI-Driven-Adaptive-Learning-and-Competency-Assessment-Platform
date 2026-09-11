using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Dashboards;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Dashboards;

public sealed class GetStudentDashboardUseCase : IGetStudentDashboardUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public GetStudentDashboardUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<StudentDashboardResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty)
        {
            return StudentDashboardResult.NotFound();
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        if (subjectId == Guid.Empty)
        {
            return StudentDashboardResult.ValidationFailed();
        }

        var student = await _dbContext.Students.AsNoTracking()
            .Where(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted)
            .Select(s => new { s.StudentId, s.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (student == null)
        {
            return StudentDashboardResult.NotFound();
        }

        var subject = await _dbContext.Subjects.AsNoTracking()
            .Where(s => s.CenterId == centerId && s.SubjectId == subjectId && !s.IsDeleted)
            .Select(s => new { s.SubjectId, s.SubjectName })
            .FirstOrDefaultAsync(cancellationToken);

        if (subject == null)
        {
            return StudentDashboardResult.NotFound();
        }

        // 1. Governed Goal & Risk State (Amendment 4: read persisted fields directly)
        var goalEntity = await _dbContext.StudentSubjectGoals.AsNoTracking()
            .Where(g => g.CenterId == centerId && g.StudentId == studentId && g.SubjectId == subjectId && !g.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        var goalDto = new StudentGoalSummaryDto
        {
            TargetScore = goalEntity?.TargetScore ?? 0m,
            RemainingDays = goalEntity?.RemainingDays ?? 0u,
            CurrentPredictedScore = goalEntity?.CurrentPredictedScore ?? 0m,
            RiskScore = goalEntity?.RiskScore ?? 0m
        };

        // 2. Topic Mastery Radar (zero-fill missing KnowledgeTwin)
        var activeTopics = await _dbContext.KnowledgeNodes.AsNoTracking()
            .Where(n => n.CenterId == centerId && n.SubjectId == subjectId && n.IsActive && !n.IsDeleted)
            .OrderBy(n => n.OrderIndex)
            .Select(n => new { n.NodeId, n.NodeName, n.ExamImportance })
            .ToListAsync(cancellationToken);

        var activeTopicIds = activeTopics.Select(t => t.NodeId).ToList();

        var twins = await _dbContext.KnowledgeTwins.AsNoTracking()
            .Where(kt => kt.CenterId == centerId &&
                         kt.StudentId == studentId &&
                         kt.SubjectId == subjectId &&
                         activeTopicIds.Contains(kt.TopicNodeId) &&
                         !kt.IsDeleted)
            .ToDictionaryAsync(kt => kt.TopicNodeId, kt => kt.MasteryPercentage, cancellationToken);

        var masteryRadar = activeTopics.Select(t => new TopicMasteryRadarDto
        {
            TopicNodeId = t.NodeId.ToString(CultureInfo.InvariantCulture),
            TopicName = t.NodeName,
            Mastery = twins.TryGetValue(t.NodeId, out var mastery) ? mastery : 0m
        }).ToList();

        // 3. ProgressLine Reconstruction (Amendment 5: deterministic topic history reconstruction)
        var topicWeights = activeTopics.ToDictionary(t => t.NodeId, t => t.ExamImportance);
        var totalWeight = activeTopics.Sum(t => t.ExamImportance);

        var progressLine = new List<SubjectProgressPointDto>();

        if (totalWeight > 0m && activeTopics.Count > 0)
        {
            var historyItems = await _dbContext.TwinUpdateHistories.AsNoTracking()
                .Where(h => h.CenterId == centerId &&
                            h.StudentId == studentId &&
                            h.SubjectId == subjectId &&
                            activeTopicIds.Contains(h.TopicNodeId))
                .OrderBy(h => h.CreatedAt)
                .ThenBy(h => h.HistoryId)
                .Select(h => new { h.TopicNodeId, h.NewMastery, h.CreatedAt })
                .ToListAsync(cancellationToken);

            var latestMasteryByTopic = activeTopics.ToDictionary(t => t.NodeId, _ => 0m);

            foreach (var item in historyItems)
            {
                latestMasteryByTopic[item.TopicNodeId] = item.NewMastery;

                var currentWeightedSum = latestMasteryByTopic.Sum(kvp => kvp.Value * topicWeights[kvp.Key]);
                var overallSubjectMastery = Math.Round(currentWeightedSum / totalWeight, 2, MidpointRounding.AwayFromZero);

                progressLine.Add(new SubjectProgressPointDto
                {
                    RecordedAt = item.CreatedAt,
                    OverallSubjectMastery = overallSubjectMastery
                });
            }
        }

        // 4. Opportunity Action (Active Recommendation)
        var activeRec = await _dbContext.Recommendations.AsNoTracking()
            .Where(r => r.CenterId == centerId &&
                        r.StudentId == studentId &&
                        r.SubjectId == subjectId &&
                        r.Status == RecommendationStatus.Active &&
                        !r.IsDeleted)
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => new
            {
                r.RecommendationType,
                r.TopicNodeId,
                TopicName = r.TopicNode.NodeName,
                r.QuestionId,
                r.OpportunityScore,
                r.Explanation
            })
            .FirstOrDefaultAsync(cancellationToken);

        StudentOpportunityActionDto? actionDto = null;
        if (activeRec != null)
        {
            actionDto = new StudentOpportunityActionDto
            {
                Strategy = activeRec.RecommendationType.ToString(),
                TopicNodeId = activeRec.TopicNodeId.ToString(CultureInfo.InvariantCulture),
                TopicName = activeRec.TopicName,
                QuestionId = activeRec.QuestionId?.ToString(CultureInfo.InvariantCulture),
                OpportunityScore = activeRec.OpportunityScore,
                Explanation = activeRec.Explanation
            };
        }

        var resultDto = new StudentDashboardDataDto
        {
            Student = new StudentBasicInfoDto
            {
                StudentId = student.StudentId,
                FullName = student.FullName
            },
            Subject = new SubjectBasicInfoDto
            {
                SubjectId = subject.SubjectId,
                SubjectName = subject.SubjectName
            },
            Goal = goalDto,
            MasteryRadar = masteryRadar,
            ProgressLine = progressLine,
            Action = actionDto,
            GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        return StudentDashboardResult.Success(resultDto);
    }
}
