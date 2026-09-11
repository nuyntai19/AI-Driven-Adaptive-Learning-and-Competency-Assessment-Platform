using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class RecommendationStateMachineTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    public RecommendationStateMachineTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: $"RecStateMachineTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.Initialize(_centerId, _studentId, "Student", 1);
        _dbContext = new EduTwinDbContext(options, _tenantContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Accept_TransitionsToAccepted_KeepsPathItemCurrent_IsIdempotent()
    {
        var path = new LearningPath
        {
            LearningPathId = Guid.NewGuid(),
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            Strategy = LearningPathStrategy.OpportunityGap,
            Status = LearningPathStatus.Active,
            Version = 1,
            GeneratedAt = _utcNow,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow,
            Items = new List<LearningPathItem>
            {
                new() { LearningPathItemId = 101, CenterId = _centerId, TopicNodeId = 1, RankOrder = 1, Status = LearningPathItemStatus.Current, Reason = "Top 1", CreatedAt = _utcNow, UpdatedAt = _utcNow },
                new() { LearningPathItemId = 102, CenterId = _centerId, TopicNodeId = 2, RankOrder = 2, Status = LearningPathItemStatus.Pending, Reason = "Top 2", CreatedAt = _utcNow, UpdatedAt = _utcNow }
            }
        };

        var rec = new Recommendation
        {
            RecommendationId = 1001,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 1,
            RecommendationType = RecommendationType.TopicAndQuestion,
            CalculationVersion = "opportunity-v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Top 1 explanation",
            Status = RecommendationStatus.Active,
            GeneratedAt = _utcNow,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        _dbContext.LearningPaths.Add(path);
        _dbContext.Recommendations.Add(rec);
        await _dbContext.SaveChangesAsync();
        var originalRecommendationRowVersion = rec.RowVersion;

        var engine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        // First Accept
        var res1 = await engine.AcceptAsync(_centerId, _studentId, rec.RecommendationId, _utcNow, CancellationToken.None);
        Assert.True(res1.Success);
        Assert.Equal(RecommendationStatus.Accepted, res1.Recommendation!.Status);
        Assert.Equal(originalRecommendationRowVersion + 1, res1.Recommendation.RowVersion);

        // Path item retains Current
        var item1 = path.Items.First(i => i.RankOrder == 1);
        Assert.Equal(LearningPathItemStatus.Current, item1.Status);

        // Second Accept (Idempotent)
        var res2 = await engine.AcceptAsync(_centerId, _studentId, rec.RecommendationId, _utcNow, CancellationToken.None);
        Assert.True(res2.Success);
        Assert.Equal(RecommendationStatus.Accepted, res2.Recommendation!.Status);
    }

    [Fact]
    public async Task Dismiss_TransitionsToDismissed_PromotesNextPendingToCurrent_CreatesNewActiveRec()
    {
        // Setup topic nodes
        var node1 = new KnowledgeNode { CenterId = _centerId, SubjectId = _subjectId, NodeId = 1, NodeCode = "N1", NodeName = "Node 1", NodeType = NodeType.Topic, OrderIndex = 1, IsActive = true, CreatedAt = _utcNow, UpdatedAt = _utcNow };
        var node2 = new KnowledgeNode { CenterId = _centerId, SubjectId = _subjectId, NodeId = 2, NodeCode = "N2", NodeName = "Node 2", NodeType = NodeType.Topic, OrderIndex = 2, IsActive = true, CreatedAt = _utcNow, UpdatedAt = _utcNow };
        _dbContext.KnowledgeNodes.AddRange(node1, node2);

        var q2 = new Question { CenterId = _centerId, SubjectId = _subjectId, PrimaryTopicNodeId = 2, QuestionId = 202, QuestionText = "Q2", CorrectAnswer = "A", Solution = "S", LanguageCode = "vi", Status = QuestionStatus.Active, MaxScore = 10, CreatedAt = _utcNow, UpdatedAt = _utcNow };
        _dbContext.Questions.Add(q2);

        var path = new LearningPath
        {
            LearningPathId = Guid.NewGuid(),
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            Strategy = LearningPathStrategy.OpportunityGap,
            Status = LearningPathStatus.Active,
            Version = 1,
            GeneratedAt = _utcNow,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow,
            Items = new List<LearningPathItem>
            {
                new() { LearningPathItemId = 201, CenterId = _centerId, TopicNodeId = 1, RankOrder = 1, Status = LearningPathItemStatus.Current, Reason = "Top 1 Reason", OpportunityScore = 90m, RecommendedQuestionId = 201ul, CreatedAt = _utcNow, UpdatedAt = _utcNow },
                new() { LearningPathItemId = 202, CenterId = _centerId, TopicNodeId = 2, RankOrder = 2, Status = LearningPathItemStatus.Pending, Reason = "Top 2 Reason", OpportunityScore = 75m, RecommendedQuestionId = 202ul, CalculationBreakdown = JsonDocument.Parse("{\"TopicNodeId\":2}"), CreatedAt = _utcNow, UpdatedAt = _utcNow }
            }
        };

        var rec = new Recommendation
        {
            RecommendationId = 2001,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 1,
            RecommendationType = RecommendationType.TopicAndQuestion,
            CalculationVersion = "opportunity-v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Top 1 explanation",
            Status = RecommendationStatus.Active,
            GeneratedAt = _utcNow,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        _dbContext.LearningPaths.Add(path);
        _dbContext.Recommendations.Add(rec);
        await _dbContext.SaveChangesAsync();
        var originalDismissedRecommendationRowVersion = rec.RowVersion;

        var engine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        // Dismiss
        var res = await engine.DismissAsync(_centerId, _studentId, rec.RecommendationId, "Too difficult", _utcNow, CancellationToken.None);
        Assert.True(res.Success);
        Assert.Equal(RecommendationStatus.Dismissed, res.Recommendation!.Status);
        Assert.Equal("Too difficult", res.Recommendation.DismissReason);
        Assert.Equal(originalDismissedRecommendationRowVersion + 1, res.Recommendation.RowVersion);

        var retry = await engine.DismissAsync(
            _centerId,
            _studentId,
            rec.RecommendationId,
            "A retry must not rewrite the original reason",
            _utcNow.AddSeconds(1),
            CancellationToken.None);
        Assert.True(retry.Success);
        Assert.Equal("Too difficult", retry.Recommendation!.DismissReason);

        // Path item 1 -> Skipped
        var item1 = path.Items.First(i => i.RankOrder == 1);
        Assert.Equal(LearningPathItemStatus.Skipped, item1.Status);

        // Path item 2 -> Current
        var item2 = path.Items.First(i => i.RankOrder == 2);
        Assert.Equal(LearningPathItemStatus.Current, item2.Status);
        Assert.Equal(202ul, item2.RecommendedQuestionId);

        // A new active recommendation was generated for item 2
        var newActiveRec = await _dbContext.Recommendations
            .FirstOrDefaultAsync(r => r.CenterId == _centerId && r.StudentId == _studentId && r.Status == RecommendationStatus.Active);
        Assert.NotNull(newActiveRec);
        Assert.Equal(2ul, newActiveRec.TopicNodeId);
        Assert.Equal(202ul, newActiveRec.QuestionId);
        Assert.Equal(2ul, newActiveRec.CalculationBreakdown.RootElement.GetProperty("TopicNodeId").GetUInt64());

        // Dismiss item 2 (last item) -> LearningPath completes
        var resDismissLast = await engine.DismissAsync(_centerId, _studentId, newActiveRec.RecommendationId, "Skip last", _utcNow, CancellationToken.None);
        Assert.True(resDismissLast.Success);
        Assert.Equal(LearningPathStatus.Completed, path.Status);
    }

    [Fact]
    public async Task NewerGeneratedThenAccepted_DelayedOlderTrigger_IsIgnoredByDurableWatermark()
    {
        await AddSingleTopicAsync();
        var engine = CreateEngine();
        var newer = await engine.GenerateAndPersistAsync(
            _centerId,
            _studentId,
            _subjectId,
            sourceAttemptId: 20,
            _utcNow,
            CancellationToken.None);
        Assert.Equal(RecommendationGenerationStatus.Generated, newer.Status);

        var accepted = await engine.AcceptAsync(
            _centerId,
            _studentId,
            newer.Recommendation!.RecommendationId,
            _utcNow.AddSeconds(1),
            CancellationToken.None);
        Assert.True(accepted.Success);

        var stale = await engine.GenerateAndPersistAsync(
            _centerId,
            _studentId,
            _subjectId,
            sourceAttemptId: 10,
            _utcNow.AddMinutes(-1),
            CancellationToken.None);

        Assert.Equal(RecommendationGenerationStatus.StaleIgnored, stale.Status);
        Assert.Empty(_dbContext.Recommendations.Where(r => r.Status == RecommendationStatus.Active));
        Assert.Single(_dbContext.Recommendations.Where(r => r.Status == RecommendationStatus.Accepted));
    }

    [Fact]
    public async Task NewerNoCandidate_DelayedOlderTrigger_IsIgnoredAfterTopicsAppear()
    {
        var engine = CreateEngine();
        var newer = await engine.GenerateAndPersistAsync(
            _centerId,
            _studentId,
            _subjectId,
            sourceAttemptId: 20,
            _utcNow,
            CancellationToken.None);
        Assert.Equal(RecommendationGenerationStatus.NoCandidate, newer.Status);

        await AddSingleTopicAsync();
        var stale = await engine.GenerateAndPersistAsync(
            _centerId,
            _studentId,
            _subjectId,
            sourceAttemptId: 10,
            _utcNow.AddMinutes(-1),
            CancellationToken.None);

        Assert.Equal(RecommendationGenerationStatus.StaleIgnored, stale.Status);
        Assert.Empty(_dbContext.Recommendations);
        var watermark = Assert.Single(_dbContext.RecommendationGenerationStates);
        Assert.Equal("NoCandidate", watermark.LastOutcome);
        Assert.Equal(20ul, watermark.LastSourceAttemptId);
    }

    [Fact]
    public async Task NewerBlocked_DelayedOlderTrigger_IsIgnoredAfterGraphIsRepaired()
    {
        _dbContext.KnowledgeNodes.AddRange(
            new KnowledgeNode
            {
                CenterId = _centerId,
                SubjectId = _subjectId,
                NodeId = 31,
                NodeCode = "B31",
                NodeName = "Blocked 31",
                NodeType = NodeType.Topic,
                OrderIndex = 1,
                IsActive = true,
                CreatedAt = _utcNow,
                UpdatedAt = _utcNow
            },
            new KnowledgeNode
            {
                CenterId = _centerId,
                SubjectId = _subjectId,
                NodeId = 32,
                NodeCode = "B32",
                NodeName = "Blocked 32",
                NodeType = NodeType.Topic,
                OrderIndex = 2,
                IsActive = true,
                CreatedAt = _utcNow,
                UpdatedAt = _utcNow
            });
        _dbContext.KnowledgeEdges.AddRange(
            new KnowledgeEdge
            {
                CenterId = _centerId,
                EdgeId = 310,
                SubjectId = _subjectId,
                SourceNodeId = 31,
                TargetNodeId = 32,
                RelationType = RelationType.PrerequisiteOf,
                Weight = 1m,
                CreatedAt = _utcNow,
                UpdatedAt = _utcNow
            },
            new KnowledgeEdge
            {
                CenterId = _centerId,
                EdgeId = 320,
                SubjectId = _subjectId,
                SourceNodeId = 32,
                TargetNodeId = 31,
                RelationType = RelationType.PrerequisiteOf,
                Weight = 1m,
                CreatedAt = _utcNow,
                UpdatedAt = _utcNow
            });
        await _dbContext.SaveChangesAsync();

        var engine = CreateEngine();
        var newer = await engine.GenerateAndPersistAsync(
            _centerId,
            _studentId,
            _subjectId,
            sourceAttemptId: 20,
            _utcNow,
            CancellationToken.None);
        Assert.Equal(RecommendationGenerationStatus.Blocked, newer.Status);

        _dbContext.KnowledgeEdges.RemoveRange(_dbContext.KnowledgeEdges);
        await _dbContext.SaveChangesAsync();
        var stale = await engine.GenerateAndPersistAsync(
            _centerId,
            _studentId,
            _subjectId,
            sourceAttemptId: 10,
            _utcNow.AddMinutes(-5),
            CancellationToken.None);

        Assert.Equal(RecommendationGenerationStatus.StaleIgnored, stale.Status);
        Assert.Empty(_dbContext.Recommendations);
        var watermark = Assert.Single(_dbContext.RecommendationGenerationStates);
        Assert.Equal("Blocked", watermark.LastOutcome);
    }

    [Fact]
    public async Task Supersede_PreservesAcceptedAndDismissedHistory()
    {
        // 1 Accepted recommendation
        var acceptedRec = new Recommendation
        {
            RecommendationId = 3001,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 1,
            RecommendationType = RecommendationType.TopicAndQuestion,
            CalculationVersion = "opportunity-v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Accepted explanation",
            Status = RecommendationStatus.Accepted,
            GeneratedAt = _utcNow.AddHours(-2),
            CreatedAt = _utcNow.AddHours(-2),
            UpdatedAt = _utcNow.AddHours(-2)
        };

        // 1 Dismissed recommendation
        var dismissedRec = new Recommendation
        {
            RecommendationId = 3002,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 2,
            RecommendationType = RecommendationType.TopicAndQuestion,
            CalculationVersion = "opportunity-v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Dismissed explanation",
            Status = RecommendationStatus.Dismissed,
            GeneratedAt = _utcNow.AddHours(-1),
            CreatedAt = _utcNow.AddHours(-1),
            UpdatedAt = _utcNow.AddHours(-1)
        };

        // 1 Active recommendation
        var activeRec = new Recommendation
        {
            RecommendationId = 3003,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 3,
            RecommendationType = RecommendationType.TopicAndQuestion,
            CalculationVersion = "opportunity-v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Active explanation",
            Status = RecommendationStatus.Active,
            GeneratedAt = _utcNow.AddMinutes(-10),
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };

        _dbContext.Recommendations.AddRange(acceptedRec, dismissedRec, activeRec);

        // Add topic node for generation
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = 4,
            NodeCode = "N4",
            NodeName = "Node 4",
            NodeType = NodeType.Topic,
            OrderIndex = 4,
            IsActive = true,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        await _dbContext.SaveChangesAsync();

        var engine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        // Generate new recommendation
        var genResult = await engine.GenerateAndPersistAsync(_centerId, _studentId, _subjectId, sourceAttemptId: 99, _utcNow, CancellationToken.None);
        Assert.Equal(RecommendationGenerationStatus.Generated, genResult.Status);

        // Verify reloaded from database (since ChangeTracker was cleared):
        var reloadedAccepted = await _dbContext.Recommendations.FindAsync(acceptedRec.RecommendationId);
        var reloadedDismissed = await _dbContext.Recommendations.FindAsync(dismissedRec.RecommendationId);
        var reloadedActive = await _dbContext.Recommendations.FindAsync(activeRec.RecommendationId);

        // Accepted is still Accepted!
        Assert.Equal(RecommendationStatus.Accepted, reloadedAccepted!.Status);
        // Dismissed is still Dismissed!
        Assert.Equal(RecommendationStatus.Dismissed, reloadedDismissed!.Status);
        // Previously Active was superseded!
        Assert.Equal(RecommendationStatus.Superseded, reloadedActive!.Status);
    }

    [Fact]
    public async Task GetNextQuestion_ContinuesResolvingCurrentItem_AfterRecommendationIsAccepted()
    {
        var node = new KnowledgeNode { CenterId = _centerId, SubjectId = _subjectId, NodeId = 10, NodeCode = "N10", NodeName = "Calculus", NodeType = NodeType.Topic, OrderIndex = 1, IsActive = true, CreatedAt = _utcNow, UpdatedAt = _utcNow };
        _dbContext.KnowledgeNodes.Add(node);

        var q = new Question { CenterId = _centerId, SubjectId = _subjectId, PrimaryTopicNodeId = 10, QuestionId = 1010, QuestionText = "Integral of x", CorrectAnswer = "A", Solution = "S", LanguageCode = "vi", Status = QuestionStatus.Active, MaxScore = 10, CreatedAt = _utcNow, UpdatedAt = _utcNow };
        _dbContext.Questions.Add(q);

        var path = new LearningPath
        {
            LearningPathId = Guid.NewGuid(),
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            Strategy = LearningPathStrategy.OpportunityGap,
            Status = LearningPathStatus.Active,
            Version = 1,
            GeneratedAt = _utcNow,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow,
            Items = new List<LearningPathItem>
            {
                new() { LearningPathItemId = 401, CenterId = _centerId, TopicNodeId = 10, RecommendedQuestionId = 1010, RankOrder = 1, Status = LearningPathItemStatus.Current, Reason = "Calculus reason", CreatedAt = _utcNow, UpdatedAt = _utcNow }
            }
        };

        var rec = new Recommendation
        {
            RecommendationId = 4001,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 10,
            QuestionId = 1010,
            RecommendationType = RecommendationType.TopicAndQuestion,
            CalculationVersion = "opportunity-v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Calculus reason",
            Status = RecommendationStatus.Active,
            GeneratedAt = _utcNow,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        _dbContext.LearningPaths.Add(path);
        _dbContext.Recommendations.Add(rec);
        await _dbContext.SaveChangesAsync();

        var engine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        // Accept recommendation
        await engine.AcceptAsync(_centerId, _studentId, rec.RecommendationId, _utcNow, CancellationToken.None);

        // Next question resolves correctly from Current path item
        var nextQ = await engine.GetNextQuestionAsync(_centerId, _studentId, _subjectId, _utcNow, CancellationToken.None);

        Assert.NotNull(nextQ);
        Assert.Equal(10ul, nextQ.Topic.NodeId);
        Assert.Equal(1010ul, nextQ.Question!.QuestionId);
        Assert.Equal(rec.RecommendationId, nextQ.RecommendationId);
    }

    private RecommendationEngine CreateEngine() =>
        new(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

    private async Task AddSingleTopicAsync()
    {
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = 900,
            NodeCode = "N900",
            NodeName = "Durable watermark topic",
            NodeType = NodeType.Topic,
            OrderIndex = 1,
            ExamImportance = 50m,
            EstimatedLearningMinutes = 60,
            IsActive = true,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });
        await _dbContext.SaveChangesAsync();
    }
}
