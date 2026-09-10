using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin;

public sealed class StudentGoalRiskUpdater : IStudentGoalRiskUpdater
{
    private readonly EduTwinDbContext _dbContext;

    public StudentGoalRiskUpdater(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<StudentSubjectGoal?> UpdateAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var goal = await _dbContext.StudentSubjectGoals
            .SingleOrDefaultAsync(
                g => g.CenterId == centerId
                    && g.StudentId == studentId
                    && g.SubjectId == subjectId
                    && !g.IsDeleted,
                cancellationToken);

        if (goal is null)
        {
            return null;
        }

        var topics = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.CenterId == centerId
                && n.SubjectId == subjectId
                && n.NodeType == NodeType.Topic
                && n.IsActive
                && !n.IsDeleted)
            .Select(n => new { n.NodeId, n.ExamImportance })
            .ToListAsync(cancellationToken);

        decimal predictedScore = 0m;

        if (topics.Count > 0)
        {
            var topicIds = topics.Select(t => t.NodeId).ToArray();
            var dbTwins = await _dbContext.KnowledgeTwins
                .Where(k => k.CenterId == centerId
                    && k.StudentId == studentId
                    && k.SubjectId == subjectId
                    && topicIds.Contains(k.TopicNodeId)
                    && !k.IsDeleted)
                .ToListAsync(cancellationToken);

            var localTwins = _dbContext.KnowledgeTwins.Local
                .Where(k => k.CenterId == centerId
                    && k.StudentId == studentId
                    && k.SubjectId == subjectId
                    && topicIds.Contains(k.TopicNodeId)
                    && !k.IsDeleted);

            var twins = dbTwins
                .UnionBy(localTwins, k => k.TopicNodeId)
                .ToDictionary(k => k.TopicNodeId, k => k.MasteryPercentage);

            decimal totalWeight = 0m;
            decimal weightedMasterySum = 0m;

            foreach (var topic in topics)
            {
                var weight = topic.ExamImportance > 0m ? topic.ExamImportance : 1m;
                var mastery = twins.GetValueOrDefault(topic.NodeId, 0m);

                totalWeight += weight;
                weightedMasterySum += mastery * weight;
            }

            var weightedAverageMastery = totalWeight > 0m
                ? weightedMasterySum / totalWeight
                : 0m;

            predictedScore = Math.Clamp(10m * weightedAverageMastery / 100m, 0m, 10m);
            predictedScore = Math.Round(predictedScore, 2, MidpointRounding.AwayFromZero);
        }

        var riskScore = StudentSubjectGoalRiskCalculator.CalculateRisk(
            goal.TargetScore,
            predictedScore,
            (int)goal.RemainingDays);

        goal.CurrentPredictedScore = predictedScore;
        goal.RiskScore = riskScore;
        goal.UpdatedAt = utcNow;

        return goal;
    }
}
