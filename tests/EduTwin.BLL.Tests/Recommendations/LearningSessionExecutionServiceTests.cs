using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningSessionExecutionServiceTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly LearningSessionExecutionService _service;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTime _now = DateTime.UtcNow;

    public LearningSessionExecutionServiceTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: $"LearningSessionTests_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var tenantContext = new TenantContext();
        tenantContext.Initialize(_centerId, _studentId, "Student", 1);
        _dbContext = new EduTwinDbContext(options, tenantContext);
        _service = new LearningSessionExecutionService(_dbContext, NullLogger<LearningSessionExecutionService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Theory]
    [InlineData(SessionRunStatus.InProgress, SessionRunStatus.Grading)]
    [InlineData(SessionRunStatus.InProgress, SessionRunStatus.Abandoned)]
    [InlineData(SessionRunStatus.Grading, SessionRunStatus.Completed)]
    [InlineData(SessionRunStatus.Grading, SessionRunStatus.NeedsReview)]
    [InlineData(SessionRunStatus.NeedsReview, SessionRunStatus.Completed)]
    [InlineData(SessionRunStatus.NeedsReview, SessionRunStatus.Abandoned)]
    public void ValidateRunTransition_ValidTransitions_ShouldSucceed(SessionRunStatus current, SessionRunStatus target)
    {
        // Act & Assert (should not throw)
        LearningSessionExecutionService.ValidateRunTransition(current, target);
    }

    [Theory]
    [InlineData(SessionRunStatus.InProgress, SessionRunStatus.Completed)] // Direct completion without grading is disallowed
    [InlineData(SessionRunStatus.Grading, SessionRunStatus.InProgress)] // Cannot revert to in-progress
    [InlineData(SessionRunStatus.Completed, SessionRunStatus.InProgress)] // Terminal state
    [InlineData(SessionRunStatus.Completed, SessionRunStatus.Grading)] // Terminal state
    [InlineData(SessionRunStatus.Abandoned, SessionRunStatus.InProgress)] // Terminal state
    public void ValidateRunTransition_InvalidTransitions_ShouldThrow(SessionRunStatus current, SessionRunStatus target)
    {
        // Act & Assert
        Assert.Throws<SessionStateTransitionException>(() =>
            LearningSessionExecutionService.ValidateRunTransition(current, target));
    }

    [Theory]
    [InlineData(LearningPathSessionStatus.NotStarted, LearningPathSessionStatus.InProgress)]
    [InlineData(LearningPathSessionStatus.NotStarted, LearningPathSessionStatus.Skipped)]
    [InlineData(LearningPathSessionStatus.InProgress, LearningPathSessionStatus.Completed)]
    [InlineData(LearningPathSessionStatus.InProgress, LearningPathSessionStatus.NeedsReview)]
    [InlineData(LearningPathSessionStatus.InProgress, LearningPathSessionStatus.Skipped)]
    [InlineData(LearningPathSessionStatus.NeedsReview, LearningPathSessionStatus.InProgress)]
    [InlineData(LearningPathSessionStatus.NeedsReview, LearningPathSessionStatus.Completed)]
    [InlineData(LearningPathSessionStatus.NeedsReview, LearningPathSessionStatus.Skipped)]
    public void ValidateSessionTransition_ValidTransitions_ShouldSucceed(LearningPathSessionStatus current, LearningPathSessionStatus target)
    {
        // Act & Assert (should not throw)
        LearningSessionExecutionService.ValidateSessionTransition(current, target);
    }

    [Theory]
    [InlineData(LearningPathSessionStatus.NotStarted, LearningPathSessionStatus.Completed)] // Cannot skip directly to completed
    [InlineData(LearningPathSessionStatus.NotStarted, LearningPathSessionStatus.NeedsReview)] // Cannot review unstarted
    [InlineData(LearningPathSessionStatus.Completed, LearningPathSessionStatus.InProgress)] // Terminal state
    [InlineData(LearningPathSessionStatus.Completed, LearningPathSessionStatus.NotStarted)] // Terminal state
    [InlineData(LearningPathSessionStatus.Skipped, LearningPathSessionStatus.InProgress)] // Terminal state
    public void ValidateSessionTransition_InvalidTransitions_ShouldThrow(LearningPathSessionStatus current, LearningPathSessionStatus target)
    {
        // Act & Assert
        Assert.Throws<SessionStateTransitionException>(() =>
            LearningSessionExecutionService.ValidateSessionTransition(current, target));
    }

    [Fact]
    public async Task MaterializeSessionsFromPlanAsync_Idempotent_DoesNotDuplicateSessions()
    {
        // Arrange
        var pathId = Guid.NewGuid();
        var planJson = CreateSamplePlanJson(pathId, topicId: 101, questionIds: ["1001", "1002", "1003"]);
        var learningPath = new LearningPath
        {
            CenterId = _centerId,
            LearningPathId = pathId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            Strategy = LearningPathStrategy.OpportunityGap,
            Status = LearningPathStatus.Active,
            PlanJson = planJson,
            GeneratedAt = _now,
            CreatedAt = _now,
            UpdatedAt = _now
        };
        await _dbContext.LearningPaths.AddAsync(learningPath);

        // Seed eligible active questions so snapshot passes
        await SeedQuestionsAsync([1001, 1002, 1003], topicId: 101);
        await _dbContext.SaveChangesAsync();

        // Act - First Materialization
        var firstResult = await _service.MaterializeSessionsFromPlanAsync(_centerId, pathId);

        // Assert First Materialization
        Assert.Single(firstResult);
        var sessionSummary = firstResult[0];
        Assert.Equal("Session 1.1", sessionSummary.Title);
        Assert.Equal(2, sessionSummary.PracticeQuestionCount); // First 2 questions
        Assert.Equal(1, sessionSummary.CheckpointQuestionCount); // Last question is checkpoint

        var countInDb = await _dbContext.LearningPathSessions
            .CountAsync(s => s.CenterId == _centerId && s.LearningPathId == pathId);
        Assert.Equal(1, countInDb);

        // Act - Second Materialization (Idempotency check)
        var secondResult = await _service.MaterializeSessionsFromPlanAsync(_centerId, pathId);

        // Assert Second Materialization returned identical sessions without duplicating
        Assert.Single(secondResult);
        Assert.Equal(firstResult[0].SessionId, secondResult[0].SessionId);

        var countAfterSecond = await _dbContext.LearningPathSessions
            .CountAsync(s => s.CenterId == _centerId && s.LearningPathId == pathId);
        Assert.Equal(1, countAfterSecond);
    }

    [Fact]
    public async Task MaterializeSessionsFromPlanAsync_DisjointQuestions_PracticeAndCheckpointNeverOverlap()
    {
        // Arrange
        var pathId = Guid.NewGuid();
        var planJson = CreateSamplePlanJson(pathId, topicId: 202, questionIds: ["2001", "2002", "2003", "2004"]);
        var learningPath = new LearningPath
        {
            CenterId = _centerId,
            LearningPathId = pathId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            Strategy = LearningPathStrategy.OpportunityGap,
            Status = LearningPathStatus.Active,
            PlanJson = planJson,
            GeneratedAt = _now,
            CreatedAt = _now,
            UpdatedAt = _now
        };
        await _dbContext.LearningPaths.AddAsync(learningPath);
        await SeedQuestionsAsync([2001, 2002, 2003, 2004], topicId: 202);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.MaterializeSessionsFromPlanAsync(_centerId, pathId);
        var session = await _dbContext.LearningPathSessions.SingleAsync(s => s.SessionId == result[0].SessionId);

        var practiceIds = JsonSerializer.Deserialize<List<ulong>>(session.PracticeQuestionIds!.RootElement.GetRawText())!;
        var checkpointIds = JsonSerializer.Deserialize<List<ulong>>(session.CheckpointQuestionIds!.RootElement.GetRawText())!;

        // Assert: Strictly disjoint
        Assert.NotEmpty(practiceIds);
        Assert.NotEmpty(checkpointIds);
        Assert.Empty(practiceIds.Intersect(checkpointIds));
        Assert.DoesNotContain(checkpointIds[0], practiceIds);
    }

    [Fact]
    public async Task StartOrResumeRunAsync_WhenNoActiveRun_CreatesNewRun()
    {
        // Arrange
        var session = await CreatePersistedSessionAsync(practiceIds: [3001, 3002], checkpointId: 3003);

        // Act
        var runResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);

        // Assert
        Assert.False(runResult.IsResumed);
        Assert.Equal(1u, runResult.RunNumber);
        Assert.Equal(SessionRunStatus.InProgress, runResult.Status);
        Assert.Equal(SessionRunEvaluationStatus.NotStarted, runResult.EvaluationStatus);
        Assert.Equal(3001ul, runResult.CurrentQuestionId);
        Assert.Equal(3003ul, runResult.CheckpointQuestionId);

        var persistedRun = await _dbContext.LearningPathSessionRuns.SingleAsync(r => r.SessionRunId == runResult.SessionRunId);
        Assert.Equal(SessionRunStatus.InProgress, persistedRun.Status);

        var updatedSession = await _dbContext.LearningPathSessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(LearningPathSessionStatus.InProgress, updatedSession.Status);
        Assert.Equal(1u, updatedSession.TotalRuns);
        Assert.Equal(persistedRun.SessionRunId, updatedSession.CurrentRunId);
    }

    [Fact]
    public async Task StartOrResumeRunAsync_WhenActiveRunExists_ReturnsActiveRunAsResumed()
    {
        // Arrange
        var session = await CreatePersistedSessionAsync(practiceIds: [4001], checkpointId: 4002);
        var initialRun = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);

        // Act - Call Start again while run is InProgress
        var resumeResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);

        // Assert - Should return same active run with IsResumed = true
        Assert.True(resumeResult.IsResumed);
        Assert.Equal(initialRun.SessionRunId, resumeResult.SessionRunId);
        Assert.Equal(initialRun.RunNumber, resumeResult.RunNumber);

        var totalRunsInDb = await _dbContext.LearningPathSessionRuns.CountAsync(r => r.SessionId == session.SessionId);
        Assert.Equal(1, totalRunsInDb); // No duplicate active run created
    }

    [Fact]
    public async Task TransitionRunAsync_ToGrading_AndThenCompleted_UpdatesRunAndSession()
    {
        // Arrange
        var session = await CreatePersistedSessionAsync(practiceIds: [5001], checkpointId: 5002);
        var runResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);

        // Act 1: Transition InProgress -> Grading
        var gradingRun = await _service.TransitionRunAsync(_centerId, runResult.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Grading,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Processing));

        Assert.Equal(SessionRunStatus.Grading, gradingRun.Status);
        Assert.Equal(SessionRunEvaluationStatus.Processing, gradingRun.EvaluationStatus);

        // Act 2: Transition Grading -> Completed (Passed)
        var completedRun = await _service.TransitionRunAsync(_centerId, runResult.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Completed,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Completed,
            TargetOutcome: SessionRunOutcome.Passed,
            ScoreRate: 0.95m,
            QuestionsAttempted: 2,
            QuestionsCorrect: 2));

        // Assert
        Assert.Equal(SessionRunStatus.Completed, completedRun.Status);
        Assert.Equal(SessionRunOutcome.Passed, completedRun.Outcome);
        Assert.NotNull(completedRun.CompletedAt);

        var updatedSession = await _dbContext.LearningPathSessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(LearningPathSessionStatus.Completed, updatedSession.Status);
    }

    [Fact]
    public async Task TransitionRunAsync_InsufficientEvidence_PreservesStateWithoutFailing()
    {
        // Arrange
        var session = await CreatePersistedSessionAsync(practiceIds: [6001], checkpointId: 6002);
        var runResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);

        // InProgress -> Grading
        await _service.TransitionRunAsync(_centerId, runResult.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Grading,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Processing));

        // Act: Completed with InsufficientEvidence
        var completedRun = await _service.TransitionRunAsync(_centerId, runResult.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Completed,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Completed,
            TargetOutcome: SessionRunOutcome.InsufficientEvidence,
            QuestionsAttempted: 1,
            QuestionsCorrect: 0));

        // Assert
        Assert.Equal(SessionRunStatus.Completed, completedRun.Status);
        Assert.Equal(SessionRunOutcome.InsufficientEvidence, completedRun.Outcome);

        // Verify parent session is not marked Completed or Failed
        var updatedSession = await _dbContext.LearningPathSessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.NotEqual(LearningPathSessionStatus.Completed, updatedSession.Status);
    }

    private static JsonDocument CreateSamplePlanJson(Guid pathId, ulong topicId, string[] questionIds)
    {
        var planObj = new[]
        {
            new
            {
                PhaseNumber = 1u,
                PhaseName = "Foundation",
                Weeks = new[]
                {
                    new
                    {
                        WeekNumber = 1u,
                        WeekTitle = "Week 1",
                        Sessions = new[]
                        {
                            new
                            {
                                SessionId = $"{pathId:D}-w01-s01",
                                TopicNodeId = topicId.ToString(),
                                Title = "Session 1.1",
                                Objective = "Master foundational skill",
                                RecommendedQuestionIds = questionIds,
                                Status = "NotStarted",
                                RequiredCorrectRate = 0.75m
                            }
                        }
                    }
                }
            }
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(planObj));
    }

    private async Task SeedQuestionsAsync(IEnumerable<ulong> questionIds, ulong topicId)
    {
        foreach (var qid in questionIds)
        {
            await _dbContext.Questions.AddAsync(new Question
            {
                CenterId = _centerId,
                QuestionId = qid,
                SubjectId = _subjectId,
                PrimaryTopicNodeId = topicId,
                QuestionText = $"Question {qid}",
                CorrectAnswer = "A",
                Solution = "Solution",
                LanguageCode = "vi",
                Status = Contracts.CurriculumAndQuestions.QuestionStatus.Active,
                QuestionType = Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice,
                AnswerEvaluationMode = Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.TextExact,
                CreatedAt = _now,
                UpdatedAt = _now
            });
        }
    }

    private async Task<LearningPathSession> CreatePersistedSessionAsync(List<ulong> practiceIds, ulong checkpointId)
    {
        await SeedQuestionsAsync(practiceIds.Append(checkpointId), 999);
        await _dbContext.SaveChangesAsync();

        var session = new LearningPathSession
        {
            SessionId = Guid.NewGuid(),
            CenterId = _centerId,
            LearningPathId = Guid.NewGuid(),
            LegacySessionId = $"{Guid.NewGuid():D}-w01-s01",
            TopicNodeId = 999,
            PhaseNumber = 1,
            WeekNumber = 1,
            SessionOrder = 1,
            Title = "Test Session",
            Objective = "Test Objective",
            Status = LearningPathSessionStatus.NotStarted,
            PracticeQuestionIds = JsonDocument.Parse(JsonSerializer.Serialize(practiceIds)),
            CheckpointQuestionIds = JsonDocument.Parse(JsonSerializer.Serialize(new[] { checkpointId })),
            MinimumQuestions = 1,
            RequiredCorrectRate = 0.70m,
            PolicyVersion = "session-policy-v1",
            CreatedAt = _now,
            UpdatedAt = _now,
            RowVersion = 1ul
        };

        await _dbContext.LearningPathSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        return session;
    }

    [Fact]
    public async Task AC05_StartSession_IssuesCorrectInitialQuestion_AndCheckpointOmitsHints()
    {
        // Arrange: Session with practice and checkpoint questions
        var session = await CreatePersistedSessionAsync(practiceIds: [7001, 7002], checkpointId: 7003);

        // Act 1: Start Run
        var runResult = await _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());

        // Assert 1: First question is the first practice question
        Assert.Equal(7001ul, runResult.CurrentQuestionId);
        Assert.Equal(7003ul, runResult.CheckpointQuestionId);

        // Act 2: Fetch question DTO for practice question
        var practiceDto = await _service.GetQuestionDtoAsync(_centerId, session.SessionId, 7001ul, isCheckpoint: false);
        Assert.NotNull(practiceDto);
        Assert.Equal("Practice", practiceDto.ActivityType);
        Assert.Equal("Test Objective", practiceDto.Explanation);

        // Act 3: Fetch question DTO for checkpoint question
        var checkpointDto = await _service.GetQuestionDtoAsync(_centerId, session.SessionId, 7003ul, isCheckpoint: true);
        Assert.NotNull(checkpointDto);
        Assert.Equal("Checkpoint", checkpointDto.ActivityType);
        Assert.Empty(checkpointDto.Explanation); // Must NOT expose explanation/hints before submission
    }

    [Fact]
    public async Task AC06_TwoConcurrentStartRequests_ReturnIdenticalActiveRun()
    {
        // Arrange
        var session = await CreatePersistedSessionAsync(practiceIds: [7101], checkpointId: 7102);

        // Act: Execute two starts simultaneously
        var task1 = _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());
        var task2 = _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());

        var results = await Task.WhenAll(task1, task2);

        // Assert: Both return the exact same active run
        Assert.Equal(results[0].SessionRunId, results[1].SessionRunId);
        Assert.Equal(results[0].SessionId, results[1].SessionId);

        // Exactly one run in DB
        var totalRunsInDb = await _dbContext.LearningPathSessionRuns
            .CountAsync(r => r.CenterId == _centerId && r.SessionId == session.SessionId && r.StudentId == _studentId);
        Assert.Equal(1, totalRunsInDb);
    }

    [Fact]
    public async Task AC07_ResumeAfterThreeAttempts_AdvancesToFourthQuestion()
    {
        // Arrange: 3 practice questions + 1 checkpoint = 4 questions total
        var session = await CreatePersistedSessionAsync(practiceIds: [7201, 7202, 7203], checkpointId: 7204);
        var runResult = await _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());

        // Simulate 3 attempts submitted for questions 7201, 7202, 7203
        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
        {
            var qid = (ulong)(7201 + i);
            await _dbContext.Attempts.AddAsync(new Attempt
            {
                CenterId = _centerId,
                StudentId = _studentId,
                QuestionId = qid,
                SessionRunId = runResult.SessionRunId,
                FinalAnswer = "A",
                IsCorrect = true,
                ReasoningLanguage = "vi",
                CreatedAt = now.AddMinutes(i),
                UpdatedAt = now.AddMinutes(i)
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act: Resume Session
        var resumeResult = await _service.ResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());

        // Assert: Current question is advanced to 7204 (the checkpoint)
        Assert.Equal(runResult.SessionRunId, resumeResult.SessionRunId);
        Assert.Equal(7204ul, resumeResult.CurrentQuestionId);
        Assert.Equal(3, resumeResult.AttemptedQuestionIds.Count);
        Assert.Contains(7201ul, resumeResult.AttemptedQuestionIds);
        Assert.Contains(7202ul, resumeResult.AttemptedQuestionIds);
        Assert.Contains(7203ul, resumeResult.AttemptedQuestionIds);
        Assert.False(resumeResult.IsCompleted);
    }

    [Fact]
    public async Task CheckpointRetry_NeverReusesPreviouslyAttemptedCheckpoint()
    {
        // Arrange: Topic 888 has checkpoint pool: [8003, 8004]
        await SeedQuestionsAsync([8001, 8002, 8003, 8004], topicId: 888);
        await _dbContext.SaveChangesAsync();

        var session = new LearningPathSession
        {
            SessionId = Guid.NewGuid(),
            CenterId = _centerId,
            LearningPathId = Guid.NewGuid(),
            LegacySessionId = $"{Guid.NewGuid():D}-w01-s01",
            TopicNodeId = 888,
            PhaseNumber = 1,
            WeekNumber = 1,
            SessionOrder = 1,
            Title = "Retry Session",
            Objective = "Retry Objective",
            Status = LearningPathSessionStatus.NotStarted,
            PracticeQuestionIds = JsonDocument.Parse("[8001, 8002]"),
            CheckpointQuestionIds = JsonDocument.Parse("[8003, 8004]"), // 2 checkpoints available
            MinimumQuestions = 1,
            RequiredCorrectRate = 0.70m,
            PolicyVersion = "session-policy-v1",
            CreatedAt = _now,
            UpdatedAt = _now,
            RowVersion = 1ul
        };
        await _dbContext.LearningPathSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        // Act 1: Run 1 starts -> gets checkpoint 8003
        var run1 = await _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());
        Assert.Equal(8003ul, run1.CheckpointQuestionId);

        // Record attempt for 8003 in Run 1 and complete Run 1 as NotPassed
        await _dbContext.Attempts.AddAsync(new Attempt
        {
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = 8003ul,
            SessionRunId = run1.SessionRunId,
            FinalAnswer = "WRONG",
            IsCorrect = false,
            ReasoningLanguage = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        await _service.TransitionRunAsync(_centerId, run1.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Grading,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Queued));

        await _service.TransitionRunAsync(_centerId, run1.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Completed,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Completed,
            TargetOutcome: SessionRunOutcome.NotPassed));

        // Act 2: Start Run 2 (Retry)
        var run2 = await _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());

        // Assert: Run 2 MUST assign 8004, NOT 8003!
        Assert.NotEqual(run1.SessionRunId, run2.SessionRunId);
        Assert.Equal(2u, run2.RunNumber);
        Assert.Equal(8004ul, run2.CheckpointQuestionId); // New checkpoint used
        Assert.NotEqual(8003ul, run2.CheckpointQuestionId);
    }

    [Fact]
    public async Task QuestionPoolDepleted_WhenNoUnusedCheckpointsRemain_ThrowsClearExceptionWithoutFabricatingQuestions()
    {
        // Arrange: Session with only 1 checkpoint (8103)
        await SeedQuestionsAsync([8101, 8103], topicId: 777);
        await _dbContext.SaveChangesAsync();

        var session = new LearningPathSession
        {
            SessionId = Guid.NewGuid(),
            CenterId = _centerId,
            LearningPathId = Guid.NewGuid(),
            LegacySessionId = $"{Guid.NewGuid():D}-w01-s01",
            TopicNodeId = 777,
            PhaseNumber = 1,
            WeekNumber = 1,
            SessionOrder = 1,
            Title = "Depletion Session",
            Objective = "Objective",
            Status = LearningPathSessionStatus.NotStarted,
            PracticeQuestionIds = JsonDocument.Parse("[8101]"),
            CheckpointQuestionIds = JsonDocument.Parse("[8103]"), // Only 1 checkpoint in pool
            MinimumQuestions = 1,
            RequiredCorrectRate = 0.70m,
            PolicyVersion = "session-policy-v1",
            CreatedAt = _now,
            UpdatedAt = _now,
            RowVersion = 1ul
        };
        await _dbContext.LearningPathSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        // Run 1 uses 8103
        var run1 = await _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());
        Assert.Equal(8103ul, run1.CheckpointQuestionId);

        // Record attempt and complete Run 1
        await _dbContext.Attempts.AddAsync(new Attempt
        {
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = 8103ul,
            SessionRunId = run1.SessionRunId,
            FinalAnswer = "WRONG",
            IsCorrect = false,
            ReasoningLanguage = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        await _service.TransitionRunAsync(_centerId, run1.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Grading,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Queued));

        await _service.TransitionRunAsync(_centerId, run1.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Completed,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Completed,
            TargetOutcome: SessionRunOutcome.NotPassed));

        // Act & Assert: Run 2 (Retry) must fail with QuestionPoolDepletedException
        var ex = await Assert.ThrowsAsync<QuestionPoolDepletedException>(() =>
            _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString()));

        Assert.Contains("không đủ câu hỏi checkpoint mới", ex.Message);
    }

    [Fact]
    public async Task GetQuestionDtoAsync_WhenQuestionIsArchived_ReturnsNull()
    {
        // Arrange: Question is marked as Archived
        var qid = 9901ul;
        await _dbContext.Questions.AddAsync(new Question
        {
            CenterId = _centerId,
            QuestionId = qid,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 999,
            QuestionText = "Archived Question",
            CorrectAnswer = "A",
            Solution = "Solution",
            LanguageCode = "vi",
            Status = Contracts.CurriculumAndQuestions.QuestionStatus.Archived, // ARCHIVED!
            QuestionType = Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice,
            AnswerEvaluationMode = Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.TextExact,
            CreatedAt = _now,
            UpdatedAt = _now
        });
        await _dbContext.SaveChangesAsync();

        // Act: Attempt to fetch question DTO
        var questionDto = await _service.GetQuestionDtoAsync(_centerId, Guid.NewGuid(), qid, isCheckpoint: false);

        // Assert: Must NOT be issued
        Assert.Null(questionDto);
    }
}
