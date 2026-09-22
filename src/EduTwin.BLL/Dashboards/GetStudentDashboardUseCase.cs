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
using EduTwin.Contracts.KnowledgeGraph;
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

    public async Task<StudentDashboardResult> ExecuteAsync(Guid? subjectId, CancellationToken cancellationToken)
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

        var student = await _dbContext.Students.AsNoTracking()
            .Where(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted)
            .Select(s => new { s.StudentId, s.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (student == null)
        {
            return StudentDashboardResult.NotFound();
        }

        // Branch A: When subjectId is null or empty, aggregate data across ALL subjects
        if (!subjectId.HasValue || subjectId.Value == Guid.Empty)
        {
            var activeSubjects = await _dbContext.Subjects.AsNoTracking()
                .Where(s => s.CenterId == centerId && s.IsActive && !s.IsDeleted)
                .OrderBy(s => s.SubjectName)
                .Select(s => new { s.SubjectId, s.SubjectName })
                .ToListAsync(cancellationToken);

            var goals = await _dbContext.StudentSubjectGoals.AsNoTracking()
                .Where(g => g.CenterId == centerId && g.StudentId == studentId && !g.IsDeleted)
                .ToListAsync(cancellationToken);

            var goalDto = new StudentGoalSummaryDto
            {
                TargetScore = goals.Count > 0 ? Math.Round(goals.Average(g => g.TargetScore), 1, MidpointRounding.AwayFromZero) : 8.0m,
                RemainingDays = goals.Count > 0 ? goals.Min(g => g.RemainingDays) : 90u,
                CurrentPredictedScore = goals.Count > 0 ? Math.Round(goals.Average(g => g.CurrentPredictedScore), 1, MidpointRounding.AwayFromZero) : 0m,
                RiskScore = goals.Count > 0 ? Math.Round(goals.Max(g => g.RiskScore), 1, MidpointRounding.AwayFromZero) : 0m
            };

            // Option A: Subject-level Competency Radar across all active subjects
            var allTopics = await _dbContext.KnowledgeNodes.AsNoTracking()
                .Where(n => n.CenterId == centerId && n.NodeType == NodeType.Topic && n.IsActive && !n.IsDeleted)
                .Select(n => new { n.NodeId, n.SubjectId, n.ExamImportance })
                .ToListAsync(cancellationToken);

            var allTopicIds = allTopics.Select(t => t.NodeId).ToList();

            var allTwins = await _dbContext.KnowledgeTwins.AsNoTracking()
                .Where(kt => kt.CenterId == centerId &&
                             kt.StudentId == studentId &&
                             allTopicIds.Contains(kt.TopicNodeId) &&
                             !kt.IsDeleted)
                .ToDictionaryAsync(kt => kt.TopicNodeId, kt => kt.MasteryPercentage, cancellationToken);

            var masteryRadar = new List<TopicMasteryRadarDto>();
            foreach (var sub in activeSubjects)
            {
                var subTopics = allTopics.Where(t => t.SubjectId == sub.SubjectId).ToList();
                decimal subMastery = 0m;
                var totalImportance = subTopics.Sum(t => t.ExamImportance);
                if (totalImportance > 0m)
                {
                    var weightedSum = subTopics.Sum(t => (allTwins.TryGetValue(t.NodeId, out var m) ? m : 0m) * t.ExamImportance);
                    subMastery = Math.Round(weightedSum / totalImportance, 1, MidpointRounding.AwayFromZero);
                }
                else if (subTopics.Count > 0)
                {
                    var sum = subTopics.Sum(t => allTwins.TryGetValue(t.NodeId, out var m) ? m : 0m);
                    subMastery = Math.Round(sum / subTopics.Count, 1, MidpointRounding.AwayFromZero);
                }

                masteryRadar.Add(new TopicMasteryRadarDto
                {
                    TopicNodeId = sub.SubjectId.ToString(),
                    TopicName = sub.SubjectName,
                    Mastery = subMastery
                });
            }

            // Progress line across all subjects
            var progressLine = new List<SubjectProgressPointDto>();
            var allTotalWeight = allTopics.Sum(t => t.ExamImportance);
            if (allTotalWeight > 0m && allTopics.Count > 0)
            {
                var topicWeightsMap = allTopics.ToDictionary(t => t.NodeId, t => t.ExamImportance);
                var historyItems = await _dbContext.TwinUpdateHistories.AsNoTracking()
                    .Where(h => h.CenterId == centerId &&
                                h.StudentId == studentId &&
                                allTopicIds.Contains(h.TopicNodeId))
                    .OrderBy(h => h.CreatedAt)
                    .ThenBy(h => h.HistoryId)
                    .Select(h => new { h.TopicNodeId, h.NewMastery, h.CreatedAt })
                    .ToListAsync(cancellationToken);

                var latestMasteryByTopic = allTopics.ToDictionary(t => t.NodeId, _ => 0m);
                foreach (var item in historyItems)
                {
                    latestMasteryByTopic[item.TopicNodeId] = item.NewMastery;
                    var currentWeightedSum = latestMasteryByTopic.Sum(kvp => kvp.Value * topicWeightsMap[kvp.Key]);
                    var overallMastery = Math.Round(currentWeightedSum / allTotalWeight, 2, MidpointRounding.AwayFromZero);

                    progressLine.Add(new SubjectProgressPointDto
                    {
                        RecordedAt = item.CreatedAt,
                        OverallSubjectMastery = overallMastery
                    });
                }
            }

            // Opportunity Action: Top active recommendation across all subjects
            var activeRec = await _dbContext.Recommendations.AsNoTracking()
                .Where(r => r.CenterId == centerId &&
                            r.StudentId == studentId &&
                            r.Status == RecommendationStatus.Active &&
                            !r.IsDeleted)
                .OrderByDescending(r => r.OpportunityScore)
                .ThenByDescending(r => r.GeneratedAt)
                .Select(r => new
                {
                    r.RecommendationId,
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
                    RecommendationId = activeRec.RecommendationId.ToString(CultureInfo.InvariantCulture),
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
                    SubjectId = Guid.Empty,
                    SubjectName = "Toàn bộ"
                },
                Goal = goalDto,
                MasteryRadar = masteryRadar,
                ProgressLine = progressLine,
                Action = actionDto,
                GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            return StudentDashboardResult.Success(resultDto);
        }

        // Branch B: Single Subject Pipeline
        var targetSubjectId = subjectId.Value;
        var subject = await _dbContext.Subjects.AsNoTracking()
            .Where(s => s.CenterId == centerId && s.SubjectId == targetSubjectId && !s.IsDeleted)
            .Select(s => new { s.SubjectId, s.SubjectName })
            .FirstOrDefaultAsync(cancellationToken);

        if (subject == null)
        {
            return StudentDashboardResult.NotFound();
        }

        // 1. Governed Goal & Risk State
        var goalEntity = await _dbContext.StudentSubjectGoals.AsNoTracking()
            .Where(g => g.CenterId == centerId && g.StudentId == studentId && g.SubjectId == targetSubjectId && !g.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        var singleGoalDto = new StudentGoalSummaryDto
        {
            TargetScore = goalEntity?.TargetScore ?? 0m,
            RemainingDays = goalEntity?.RemainingDays ?? 0u,
            CurrentPredictedScore = goalEntity?.CurrentPredictedScore ?? 0m,
            RiskScore = goalEntity?.RiskScore ?? 0m
        };

        // 2. Topic Mastery Radar (zero-fill missing KnowledgeTwin)
        var activeTopics = await _dbContext.KnowledgeNodes.AsNoTracking()
            .Where(n => n.CenterId == centerId && n.SubjectId == targetSubjectId && n.NodeType == NodeType.Topic && n.IsActive && !n.IsDeleted)
            .OrderBy(n => n.OrderIndex)
            .Select(n => new { n.NodeId, n.NodeName, n.ExamImportance })
            .ToListAsync(cancellationToken);

        var activeTopicIds = activeTopics.Select(t => t.NodeId).ToList();

        var twins = await _dbContext.KnowledgeTwins.AsNoTracking()
            .Where(kt => kt.CenterId == centerId &&
                         kt.StudentId == studentId &&
                         kt.SubjectId == targetSubjectId &&
                         activeTopicIds.Contains(kt.TopicNodeId) &&
                         !kt.IsDeleted)
            .ToDictionaryAsync(kt => kt.TopicNodeId, kt => kt.MasteryPercentage, cancellationToken);

        var singleMasteryRadar = activeTopics.Select(t => new TopicMasteryRadarDto
        {
            TopicNodeId = t.NodeId.ToString(CultureInfo.InvariantCulture),
            TopicName = t.NodeName,
            Mastery = twins.TryGetValue(t.NodeId, out var mastery) ? mastery : 0m
        }).ToList();

        // 3. ProgressLine Reconstruction
        var topicWeights = activeTopics.ToDictionary(t => t.NodeId, t => t.ExamImportance);
        var totalWeight = activeTopics.Sum(t => t.ExamImportance);

        var singleProgressLine = new List<SubjectProgressPointDto>();

        if (totalWeight > 0m && activeTopics.Count > 0)
        {
            var historyItems = await _dbContext.TwinUpdateHistories.AsNoTracking()
                .Where(h => h.CenterId == centerId &&
                            h.StudentId == studentId &&
                            h.SubjectId == targetSubjectId &&
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

                singleProgressLine.Add(new SubjectProgressPointDto
                {
                    RecordedAt = item.CreatedAt,
                    OverallSubjectMastery = overallSubjectMastery
                });
            }
        }

        // 4. Opportunity Action (Active Recommendation)
        var singleActiveRec = await _dbContext.Recommendations.AsNoTracking()
            .Where(r => r.CenterId == centerId &&
                        r.StudentId == studentId &&
                        r.SubjectId == targetSubjectId &&
                        r.Status == RecommendationStatus.Active &&
                        !r.IsDeleted)
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => new
            {
                r.RecommendationId,
                r.RecommendationType,
                r.TopicNodeId,
                TopicName = r.TopicNode.NodeName,
                r.QuestionId,
                r.OpportunityScore,
                r.Explanation
            })
            .FirstOrDefaultAsync(cancellationToken);

        StudentOpportunityActionDto? singleActionDto = null;
        if (singleActiveRec != null)
        {
            singleActionDto = new StudentOpportunityActionDto
            {
                RecommendationId = singleActiveRec.RecommendationId.ToString(CultureInfo.InvariantCulture),
                Strategy = singleActiveRec.RecommendationType.ToString(),
                TopicNodeId = singleActiveRec.TopicNodeId.ToString(CultureInfo.InvariantCulture),
                TopicName = singleActiveRec.TopicName,
                QuestionId = singleActiveRec.QuestionId?.ToString(CultureInfo.InvariantCulture),
                OpportunityScore = singleActiveRec.OpportunityScore,
                Explanation = singleActiveRec.Explanation
            };
        }

        var singleResultDto = new StudentDashboardDataDto
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
            Goal = singleGoalDto,
            MasteryRadar = singleMasteryRadar,
            ProgressLine = singleProgressLine,
            Action = singleActionDto,
            GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        return StudentDashboardResult.Success(singleResultDto);
    }
}
