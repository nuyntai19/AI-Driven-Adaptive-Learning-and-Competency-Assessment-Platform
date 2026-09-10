using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.DigitalTwin.Orchestration;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.DigitalTwin;

public sealed class TwinCompletionOrchestratorTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();

    public TwinCompletionOrchestratorTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task CompleteAsync_ValidAttempt_ExecutesFullPipelineAndUpdatesAllAggregates()
    {
        var now = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

        var orchestrator = new TwinCompletionOrchestrator(
            _dbContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new BehaviorTwinUpdater(_dbContext),
            new KnowledgeTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext));

        var question = new Question
        {
            CenterId = _centerId,
            QuestionId = 501,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 101,
            Difficulty = 3,
            EstimatedTimeSeconds = 60,
            QuestionText = "Sample question",
            CorrectAnswer = "42",
            Solution = "Sample solution",
            LanguageCode = "vi",
            Status = QuestionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1001,
            StudentId = _studentId,
            QuestionId = 501,
            TimeSpentSeconds = 60,
            Confidence = 90m,
            IsCorrect = true,
            FinalAnswer = "42",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Questions.Add(question);
        _dbContext.Attempts.Add(attempt);
        await _dbContext.SaveChangesAsync();

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 2001,
            AttemptId = 1001,
            ReasoningQuality = 85m,
            AnalysisConfidence = 85m,
            Feedback = "Good reasoning",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            SchemaVersion = "1.0",
            CreatedAt = now,
            UpdatedAt = now
        };

        var result = await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.AIAnalysis,
            now,
            CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.NotNull(result);
        Assert.Equal(AttemptStatus.Completed, attempt.Status);
        Assert.NotNull(result.Evidence);
        Assert.Equal(EvidenceTrustLevel.Trusted, result.Evidence.TrustLevel);
        Assert.Equal(1.00m, result.Evidence.ReasoningWeight);

        Assert.NotNull(result.KnowledgeTwin);
        Assert.True(result.KnowledgeTwin.MasteryPercentage > 0m);

        Assert.NotNull(result.BehaviorTwin);
        Assert.Equal(1u, result.BehaviorTwin.AttemptCount);

        Assert.NotNull(result.History);
        Assert.Equal(TwinEventSource.AIAnalysis, result.History.EventSource);

        Assert.NotNull(result.StudentTwin);
        Assert.True(result.StudentTwin.OverallMastery > 0m);
    }

    [Fact]
    public async Task CompleteAsync_WithAssignment_UpdatesProgressCompletedQuestionCount()
    {
        var now = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
        var assignmentId = Guid.NewGuid();

        var progress = new StudentAssignmentProgress
        {
            CenterId = _centerId,
            AssignmentId = assignmentId,
            StudentId = _studentId,
            TotalQuestionCount = 2,
            CompletedQuestionCount = 0,
            Status = ProgressStatus.NotStarted,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.StudentAssignmentProgresses.Add(progress);
        await _dbContext.SaveChangesAsync();

        var orchestrator = new TwinCompletionOrchestrator(
            _dbContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new BehaviorTwinUpdater(_dbContext),
            new KnowledgeTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext));

        var question = new Question
        {
            CenterId = _centerId,
            QuestionId = 501,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 101,
            Difficulty = 3,
            EstimatedTimeSeconds = 60
        };

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1001,
            AssignmentId = assignmentId,
            StudentId = _studentId,
            QuestionId = 501,
            TimeSpentSeconds = 60,
            Confidence = 90m,
            IsCorrect = true,
            FinalAnswer = "42",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Attempts.Add(attempt);

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 2001,
            AttemptId = 1001,
            ReasoningQuality = 85m,
            AnalysisConfidence = 85m,
            Feedback = "Good reasoning",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            SchemaVersion = "1.0",
            CreatedAt = now,
            UpdatedAt = now
        };

        await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.AIAnalysis,
            now,
            CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.Equal(1u, progress.CompletedQuestionCount);
        Assert.Equal(ProgressStatus.InProgress, progress.Status);
    }

    [Fact]
    public async Task CompleteAsync_FailingUpdater_ThrowsAndAllowsCallerToRollback()
    {
        var now = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
        var failingGoalUpdater = new Mock<IStudentGoalRiskUpdater>();
        failingGoalUpdater
            .Setup(u => u.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated goal risk calculation failure"));

        var orchestrator = new TwinCompletionOrchestrator(
            _dbContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new BehaviorTwinUpdater(_dbContext),
            new KnowledgeTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            failingGoalUpdater.Object,
            new StudentTwinUpdater(_dbContext));

        var question = new Question
        {
            CenterId = _centerId,
            QuestionId = 502,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 101,
            Difficulty = 3,
            EstimatedTimeSeconds = 60,
            QuestionText = "Sample question 2",
            CorrectAnswer = "42",
            Solution = "Sample solution 2",
            LanguageCode = "vi",
            Status = QuestionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1002,
            StudentId = _studentId,
            QuestionId = 502,
            TimeSpentSeconds = 60,
            Confidence = 90m,
            IsCorrect = true,
            FinalAnswer = "42",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Questions.Add(question);
        _dbContext.Attempts.Add(attempt);
        await _dbContext.SaveChangesAsync();

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 2002,
            AttemptId = 1002,
            ReasoningQuality = 85m,
            AnalysisConfidence = 85m,
            Feedback = "Good reasoning",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            SchemaVersion = "1.0",
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.CompleteAsync(
                attempt,
                question,
                analysis,
                TwinEventSource.AIAnalysis,
                now,
                CancellationToken.None));

        Assert.Equal("Simulated goal risk calculation failure", exception.Message);

        await transaction.RollbackAsync();
        _dbContext.ChangeTracker.Clear();

        // Verify that after rollback, no analysis or evidence was committed
        Assert.False(await _dbContext.ReasoningAnalyses.AnyAsync(a => a.AnalysisId == 2002));
        Assert.False(await _dbContext.EvidenceAssessments.AnyAsync(e => e.AnalysisId == 2002));
        Assert.False(await _dbContext.TwinUpdateHistories.AnyAsync(h => h.AttemptId == 1002));
    }
}
