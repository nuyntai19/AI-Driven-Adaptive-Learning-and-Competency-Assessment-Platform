using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.DigitalTwin;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.DigitalTwin;

public sealed class StudentGoalRiskUpdaterTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();

    public StudentGoalRiskUpdaterTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task UpdateAsync_WithGoalAndTopics_ComputesPredictedScoreAndRiskScore()
    {
        var now = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

        // Topic 1: weight 2.0, mastery 80%
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode
        {
            CenterId = _centerId,
            NodeId = 101,
            SubjectId = _subjectId,
            NodeType = NodeType.Topic,
            NodeCode = "TOPIC-1",
            NodeName = "Functions",
            ExamImportance = 2.0m,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        // Topic 2: weight 1.0, mastery 50%
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode
        {
            CenterId = _centerId,
            NodeId = 102,
            SubjectId = _subjectId,
            NodeType = NodeType.Topic,
            NodeCode = "TOPIC-2",
            NodeName = "Calculus",
            ExamImportance = 1.0m,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        _dbContext.KnowledgeTwins.AddRange(
            new KnowledgeTwin
            {
                KnowledgeTwinId = 1,
                CenterId = _centerId,
                StudentId = _studentId,
                SubjectId = _subjectId,
                TopicNodeId = 101,
                MasteryPercentage = 80m,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            },
            new KnowledgeTwin
            {
                KnowledgeTwinId = 2,
                CenterId = _centerId,
                StudentId = _studentId,
                SubjectId = _subjectId,
                TopicNodeId = 102,
                MasteryPercentage = 50m,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });

        // Goal: target 9.0, remaining days 90
        var goal = new StudentSubjectGoal
        {
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TargetScore = 9.0m,
            RemainingDays = 90,
            CurrentPredictedScore = 0m,
            RiskScore = 0m,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.StudentSubjectGoals.Add(goal);
        await _dbContext.SaveChangesAsync();

        var updater = new StudentGoalRiskUpdater(_dbContext);
        var result = await updater.UpdateAsync(_centerId, _studentId, _subjectId, now, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.NotNull(result);
        // Weighted average: (80 * 2.0 + 50 * 1.0) / (2.0 + 1.0) = 210 / 3.0 = 70%
        // PredictedScore: 10 * 70 / 100 = 7.00
        Assert.Equal(7.00m, result.CurrentPredictedScore);
        Assert.True(result.RiskScore > 0m);
    }
}
