using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.BLL.Recommendations.UseCases;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningSessionEvaluationPhase4Tests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly LearningSessionExecutionService _service;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTime _now = DateTime.UtcNow;

    public LearningSessionEvaluationPhase4Tests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: $"EvaluationPhase4_{Guid.NewGuid():N}")
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

    private Attempt CreateAttempt(
        ulong sessionRunId,
        ulong questionId,
        bool isCorrect,
        AttemptStatus status = AttemptStatus.Completed,
        int minuteOffset = 0)
    {
        return new Attempt
        {
            CenterId = _centerId,
            StudentId = _studentId,
            SessionRunId = sessionRunId,
            QuestionId = questionId,
            FinalAnswer = isCorrect ? "A" : "B",
            IsCorrect = isCorrect,
            Status = status,
            ReasoningLanguage = "vi",
            ReasoningText = "Lập luận",
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = _now.AddMinutes(minuteOffset),
            UpdatedAt = _now.AddMinutes(minuteOffset)
        };
    }

    private async Task SeedQuestionsAsync(IEnumerable<ulong> questionIds, ulong topicId)
    {
        foreach (var id in questionIds)
        {
            if (await _dbContext.Questions.AnyAsync(q => q.CenterId == _centerId && q.QuestionId == id))
                continue;

            await _dbContext.Questions.AddAsync(new Question
            {
                CenterId = _centerId,
                QuestionId = id,
                SubjectId = _subjectId,
                PrimaryTopicNodeId = topicId,
                QuestionText = $"Question {id}",
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
        await _dbContext.SaveChangesAsync();
    }

    private async Task<LearningPathSession> CreateSessionAsync(
        List<ulong> practiceIds,
        List<ulong> checkpointIds,
        decimal requiredCorrectRate = 0.80m,
        uint minQuestions = 5)
    {
        var allIds = practiceIds.Concat(checkpointIds).Distinct().ToList();
        await SeedQuestionsAsync(allIds, topicId: 555);

        var session = new LearningPathSession
        {
            SessionId = Guid.NewGuid(),
            CenterId = _centerId,
            LearningPathId = Guid.NewGuid(),
            LegacySessionId = $"{Guid.NewGuid():D}-w01-s01",
            TopicNodeId = 555,
            PhaseNumber = 1,
            WeekNumber = 1,
            SessionOrder = 1,
            Title = "Evaluation Test Session",
            Objective = "Test Objective",
            Status = LearningPathSessionStatus.NotStarted,
            PracticeQuestionIds = JsonDocument.Parse(JsonSerializer.Serialize(practiceIds)),
            CheckpointQuestionIds = JsonDocument.Parse(JsonSerializer.Serialize(checkpointIds)),
            MinimumQuestions = minQuestions,
            RequiredCorrectRate = requiredCorrectRate,
            PolicyVersion = "session-policy-v1",
            CreatedAt = _now,
            UpdatedAt = _now,
            RowVersion = 1ul
        };

        // A session must belong to a real student-owned path, as it does under relational foreign keys.
        _dbContext.LearningPaths.Add(new LearningPath
        {
            LearningPathId = session.LearningPathId, CenterId = _centerId, StudentId = _studentId, SubjectId = _subjectId,
            Status = LearningPathStatus.Active, GeneratedAt = _now, CreatedAt = _now, UpdatedAt = _now, RowVersion = 1
        });

        await _dbContext.LearningPathSessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        return session;
    }

    // 1. Guided 4/5, checkpoint 1/5 -> NotPassed
    [Fact]
    public async Task Scenario1_GuidedFourOfFive_CheckpointOneOfFive_ReturnsNotPassed()
    {
        // Arrange: 5 practice questions and 5 checkpoint questions (Total 10)
        var practiceIds = new List<ulong> { 101, 102, 103, 104, 105 };
        var checkpointIds = new List<ulong> { 201, 202, 203, 204, 205 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds, requiredCorrectRate: 0.80m, minQuestions: 5);

        var startResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);
        var runId = startResult.SessionRunId;

        // 4 of 5 practice correct
        for (int i = 0; i < 5; i++)
        {
            await _dbContext.Attempts.AddAsync(CreateAttempt(runId, practiceIds[i], isCorrect: i < 4, minuteOffset: i));
        }

        // Only 1 of 5 checkpoint correct (20% < 80%)
        for (int i = 0; i < 5; i++)
        {
            await _dbContext.Attempts.AddAsync(CreateAttempt(runId, checkpointIds[i], isCorrect: i == 0, minuteOffset: 10 + i));
        }
        await _dbContext.SaveChangesAsync();

        // Act: Evaluate
        var result = await _service.EvaluateSessionRunAsync(_centerId, runId);

        // Assert: Must NOT pass because checkpoint correct rate is 20% < 80%
        Assert.Equal(SessionRunOutcome.NotPassed, result.Outcome);
        Assert.Equal(SessionRunStatus.Completed, result.RunStatus);
        Assert.Equal(SessionRunEvaluationStatus.Completed, result.EvaluationStatus);
        Assert.Equal(5u, result.CheckpointsAttempted);
        Assert.Equal(1u, result.CheckpointsCorrect);
        Assert.Equal(0.20m, result.CheckpointScoreRate);
        Assert.False(result.RequiresTeacherReview);
        Assert.False(result.HasPendingAIGrading);

        // Session status is NeedsReview (remediation needed)
        var updatedSession = await _dbContext.LearningPathSessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(LearningPathSessionStatus.NeedsReview, updatedSession.Status);

        // Active key released
        var updatedRun = await _dbContext.LearningPathSessionRuns.SingleAsync(r => r.SessionRunId == runId);
        Assert.Null(updatedRun.ActiveKey);
    }

    // 2. Checkpoint 5/5 Trusted -> Passed khi mọi điều kiện khác đều đạt
    [Fact]
    public async Task Scenario2_CheckpointsFiveOfFiveTrusted_ReturnsPassedAndSessionCompleted()
    {
        // Arrange: 5 practice questions and 5 checkpoint questions
        var practiceIds = new List<ulong> { 301, 302, 303, 304, 305 };
        var checkpointIds = new List<ulong> { 401, 402, 403, 404, 405 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds, requiredCorrectRate: 0.80m, minQuestions: 5);

        var startResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);
        var runId = startResult.SessionRunId;

        // Practice attempts (all correct)
        for (int i = 0; i < 5; i++)
        {
            await _dbContext.Attempts.AddAsync(CreateAttempt(runId, practiceIds[i], isCorrect: true, minuteOffset: i));
        }

        // 5 of 5 checkpoints correct and trusted
        for (int i = 0; i < 5; i++)
        {
            var attempt = CreateAttempt(runId, checkpointIds[i], isCorrect: true, minuteOffset: 10 + i);
            await _dbContext.Attempts.AddAsync(attempt);
            await _dbContext.SaveChangesAsync();

            await _dbContext.EvidenceAssessments.AddAsync(new EvidenceAssessment
            {
                CenterId = _centerId,
                AttemptId = attempt.AttemptId,
                SourceType = EvidenceSourceType.AI,
                TrustLevel = EvidenceTrustLevel.Trusted,
                DecisionMode = EvidenceDecisionMode.AIWeighted,
                ReasoningWeight = 1.0m,
                ReasonCodes = JsonDocument.Parse("[\"TRUSTED_REASONING\"]"),
                RequiresTeacherReview = false,
                PolicyVersion = "evidence-gate-v1",
                EvaluatedAt = _now.AddMinutes(10 + i),
                CreatedAt = _now.AddMinutes(10 + i)
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act: Evaluate
        var result = await _service.EvaluateSessionRunAsync(_centerId, runId);

        // Assert: Passed!
        Assert.Equal(SessionRunOutcome.Passed, result.Outcome);
        Assert.Equal(SessionRunStatus.Completed, result.RunStatus);
        Assert.Equal(SessionRunEvaluationStatus.Completed, result.EvaluationStatus);
        Assert.Equal(5u, result.CheckpointsAttempted);
        Assert.Equal(5u, result.CheckpointsCorrect);
        Assert.Equal(1.00m, result.CheckpointScoreRate);

        // Parent session transitions to Completed
        var updatedSession = await _dbContext.LearningPathSessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(LearningPathSessionStatus.Completed, updatedSession.Status);

        // Active key released
        var updatedRun = await _dbContext.LearningPathSessionRuns.SingleAsync(r => r.SessionRunId == runId);
        Assert.Null(updatedRun.ActiveKey);
    }

    // 3. AI job chưa xong -> Grading, không phải NeedsTeacherReview (EG-S11)
    [Fact]
    public async Task Scenario3_AIJobPendingOrProcessing_ReturnsGradingStatusNotNeedsTeacherReview()
    {
        // Arrange
        var practiceIds = new List<ulong> { 501, 502 };
        var checkpointIds = new List<ulong> { 503 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds, minQuestions: 3);

        var startResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);
        var runId = startResult.SessionRunId;

        // Attempts submitted, but one has AIJobStatus.Processing
        for (int i = 0; i < 3; i++)
        {
            var qid = i < 2 ? practiceIds[i] : checkpointIds[0];
            var attempt = CreateAttempt(runId, qid, isCorrect: true, status: i == 2 ? AttemptStatus.Processing : AttemptStatus.Completed, minuteOffset: i);
            await _dbContext.Attempts.AddAsync(attempt);
            await _dbContext.SaveChangesAsync();

            if (i == 2)
            {
                await _dbContext.AIAnalysisJobs.AddAsync(new AIAnalysisJob
                {
                    CenterId = _centerId,
                    AttemptId = attempt.AttemptId,
                    Status = AIJobStatus.Processing, // AI is still grading
                    AvailableAt = _now,
                    CorrelationId = Guid.NewGuid().ToString(),
                    CreatedAt = _now,
                    UpdatedAt = _now
                });
                await _dbContext.SaveChangesAsync();
            }
        }

        // Act: Evaluate
        var result = await _service.EvaluateSessionRunAsync(_centerId, runId);

        // Assert: Must be in Grading, NOT NeedsReview / NeedsTeacherReview!
        Assert.Equal(SessionRunStatus.Grading, result.RunStatus);
        Assert.Equal(SessionRunEvaluationStatus.Processing, result.EvaluationStatus);
        Assert.True(result.HasPendingAIGrading);
        Assert.False(result.RequiresTeacherReview);
        Assert.Equal(SessionRunOutcome.None, result.Outcome);
        Assert.False(result.IsCompleted);

        // Run still occupies active slot while Grading
        var updatedRun = await _dbContext.LearningPathSessionRuns.SingleAsync(r => r.SessionRunId == runId);
        Assert.Equal(1, updatedRun.ActiveKey);
    }

    // 4. AI ReviewOnly -> chờ giáo viên (EG-S12)
    [Fact]
    public async Task Scenario4_AIReviewOnly_ReturnsNeedsReviewAwaitingTeacher()
    {
        // Arrange
        var practiceIds = new List<ulong> { 601 };
        var checkpointIds = new List<ulong> { 602 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds, minQuestions: 2);

        var startResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);
        var runId = startResult.SessionRunId;

        // Practice attempt
        await _dbContext.Attempts.AddAsync(CreateAttempt(runId, 601, isCorrect: true));

        // Checkpoint attempt with ReviewOnly evidence
        var checkpointAttempt = CreateAttempt(runId, 602, isCorrect: true, minuteOffset: 1);
        await _dbContext.Attempts.AddAsync(checkpointAttempt);
        await _dbContext.SaveChangesAsync();

        await _dbContext.ReasoningAnalyses.AddAsync(new ReasoningAnalysis
        {
            CenterId = _centerId,
            AttemptId = checkpointAttempt.AttemptId,
            SchemaVersion = "1.0",
            Feedback = "Test feedback",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            NeedsTeacherReview = true, // Flags for review
            ReviewDecision = null, // Teacher has not resolved yet
            CreatedAt = _now,
            UpdatedAt = _now
        });

        await _dbContext.EvidenceAssessments.AddAsync(new EvidenceAssessment
        {
            CenterId = _centerId,
            AttemptId = checkpointAttempt.AttemptId,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.ReviewOnly,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 0.5m,
            ReasonCodes = JsonDocument.Parse("[\"AMBIGUOUS_REASONING\"]"),
            RequiresTeacherReview = true,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _now,
            CreatedAt = _now
        });
        await _dbContext.SaveChangesAsync();

        // Act: Evaluate
        var result = await _service.EvaluateSessionRunAsync(_centerId, runId);

        // Assert: Must transition to NeedsReview (AwaitingTeacher)
        Assert.Equal(SessionRunStatus.NeedsReview, result.RunStatus);
        Assert.Equal(SessionRunEvaluationStatus.AwaitingTeacher, result.EvaluationStatus);
        Assert.True(result.RequiresTeacherReview);
        Assert.False(result.HasPendingAIGrading);
        Assert.False(result.IsCompleted);

        // Parent session also transitions to NeedsReview
        var updatedSession = await _dbContext.LearningPathSessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(LearningPathSessionStatus.NeedsReview, updatedSession.Status);

        // Active slot still held while NeedsReview
        var updatedRun = await _dbContext.LearningPathSessionRuns.SingleAsync(r => r.SessionRunId == runId);
        Assert.Equal(1, updatedRun.ActiveKey);
    }

    // 5. Thiếu checkpoint -> InsufficientEvidence (EG-S13)
    [Fact]
    public async Task Scenario5_MissingCheckpoints_ReturnsInsufficientEvidence()
    {
        // Arrange: Checkpoints exist in session policy, but student only submitted practice attempts
        var practiceIds = new List<ulong> { 701, 702 };
        var checkpointIds = new List<ulong> { 703 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds, minQuestions: 2);

        var startResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);
        var runId = startResult.SessionRunId;

        // Student completed 2 practice questions, but 0 checkpoints
        for (int i = 0; i < 2; i++)
        {
            await _dbContext.Attempts.AddAsync(CreateAttempt(runId, practiceIds[i], isCorrect: true, minuteOffset: i));
        }
        await _dbContext.SaveChangesAsync();

        // Act: Evaluate
        var result = await _service.EvaluateSessionRunAsync(_centerId, runId);

        // Assert: InsufficientEvidence because 0 checkpoints were attempted
        Assert.Equal(SessionRunOutcome.InsufficientEvidence, result.Outcome);
        Assert.Equal(SessionRunStatus.Completed, result.RunStatus);
        Assert.Equal(0u, result.CheckpointsAttempted);
        Assert.Contains("thiếu câu hỏi kiểm tra độc lập", result.EvaluationDetail);

        // Parent session transitions to NeedsReview
        var updatedSession = await _dbContext.LearningPathSessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(LearningPathSessionStatus.NeedsReview, updatedSession.Status);
    }

    // 6. Retry dùng câu mới (EG-S10)
    [Fact]
    public async Task Scenario6_Retry_UsesNewCheckpointQuestion()
    {
        // Arrange: Pool has 2 checkpoints: 801 and 802
        var practiceIds = new List<ulong> { 800 };
        var checkpointIds = new List<ulong> { 801, 802 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds);

        // Run 1 starts and gets checkpoint 801
        var run1 = await _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());
        Assert.Equal(801ul, run1.CheckpointQuestionId);

        // Run 1 attempts 801 and fails
        await _dbContext.Attempts.AddAsync(CreateAttempt(run1.SessionRunId, 801ul, isCorrect: false));
        await _dbContext.SaveChangesAsync();

        // Transition InProgress -> Grading -> Completed
        await _service.TransitionRunAsync(_centerId, run1.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Grading,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Processing));

        await _service.TransitionRunAsync(_centerId, run1.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Completed,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Completed,
            TargetOutcome: SessionRunOutcome.NotPassed));

        // Act: Start Run 2 (Retry)
        var run2 = await _service.StartOrResumeSessionAsync(_centerId, _studentId, _subjectId, session.SessionId.ToString());

        // Assert: Run 2 MUST use unused checkpoint 802
        Assert.Equal(802ul, run2.CheckpointQuestionId);
        Assert.NotEqual(run1.SessionRunId, run2.SessionRunId);
    }

    // 7. Evaluate gọi lặp không đổi outcome (Idempotent)
    [Fact]
    public async Task Scenario7_EvaluateRepeatedCalls_Idempotent_DoesNotMutateOutcome()
    {
        // Arrange
        var practiceIds = new List<ulong> { 901 };
        var checkpointIds = new List<ulong> { 902 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds, minQuestions: 2);

        var startResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);
        var runId = startResult.SessionRunId;

        await _dbContext.Attempts.AddAsync(CreateAttempt(runId, 901, isCorrect: true));
        await _dbContext.Attempts.AddAsync(CreateAttempt(runId, 902, isCorrect: true, minuteOffset: 1));
        await _dbContext.SaveChangesAsync();

        // First evaluate
        var eval1 = await _service.EvaluateSessionRunAsync(_centerId, runId);
        Assert.Equal(SessionRunOutcome.Passed, eval1.Outcome);
        Assert.Equal(SessionRunStatus.Completed, eval1.RunStatus);

        var runAfterFirst = await _dbContext.LearningPathSessionRuns.AsNoTracking().SingleAsync(r => r.SessionRunId == runId);
        var completedAtFirst = runAfterFirst.CompletedAt;
        var rowVersionFirst = runAfterFirst.RowVersion;

        // Act: Call evaluate a second and third time
        var eval2 = await _service.EvaluateSessionRunAsync(_centerId, runId);
        var eval3 = await _service.EvaluateSessionRunAsync(_centerId, runId);

        // Assert: Exact same results, no mutations
        Assert.Equal(eval1.Outcome, eval2.Outcome);
        Assert.Equal(eval1.Outcome, eval3.Outcome);
        Assert.Equal(eval1.CheckpointsCorrect, eval2.CheckpointsCorrect);
        Assert.Equal(eval1.CheckpointScoreRate, eval2.CheckpointScoreRate);

        var runAfterReplay = await _dbContext.LearningPathSessionRuns.AsNoTracking().SingleAsync(r => r.SessionRunId == runId);
        Assert.Equal(completedAtFirst, runAfterReplay.CompletedAt);
        Assert.Equal(rowVersionFirst, runAfterReplay.RowVersion);
    }

    // 8. Teacher approve sau NeedsReview không cộng mastery trùng (EG-S14)
    [Fact]
    public async Task Scenario8_TeacherApprove_AfterNeedsReview_ReEvaluatesRunWithoutDoubleCountingMastery()
    {
        // Arrange: Checkpoint attempt with ReviewOnly
        var practiceIds = new List<ulong> { 1001 };
        var checkpointIds = new List<ulong> { 1002 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds, minQuestions: 2);

        var startResult = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);
        var runId = startResult.SessionRunId;

        await _dbContext.Attempts.AddAsync(CreateAttempt(runId, 1001, isCorrect: true));

        var checkpointAttempt = CreateAttempt(runId, 1002, isCorrect: true, minuteOffset: 1);
        await _dbContext.Attempts.AddAsync(checkpointAttempt);
        await _dbContext.SaveChangesAsync();

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AttemptId = checkpointAttempt.AttemptId,
            SchemaVersion = "1.0",
            Feedback = "Test feedback",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            NeedsTeacherReview = true,
            ReviewDecision = null,
            CreatedAt = _now,
            UpdatedAt = _now
        };
        await _dbContext.ReasoningAnalyses.AddAsync(analysis);
        await _dbContext.SaveChangesAsync();

        // Initial evaluate -> NeedsReview
        var initialEval = await _service.EvaluateSessionRunAsync(_centerId, runId);
        Assert.Equal(SessionRunStatus.NeedsReview, initialEval.RunStatus);

        // Teacher approves the analysis
        analysis.ReviewDecision = TeacherReviewDecision.Approved;
        analysis.ReviewedByUserId = Guid.NewGuid();
        analysis.NeedsTeacherReview = false;
        analysis.ReviewedAt = DateTime.UtcNow;
        analysis.OverrideVersion = 1;
        await _dbContext.SaveChangesAsync();

        // Act: Re-evaluate after teacher review
        var reEval = await _service.EvaluateSessionRunAsync(_centerId, runId);

        // Assert: Now Passed!
        Assert.Equal(SessionRunOutcome.Passed, reEval.Outcome);
        Assert.Equal(SessionRunStatus.Completed, reEval.RunStatus);

        // And verify digital twin update was NOT triggered by session evaluation service
        // (EvaluateSessionRunAsync does NOT insert into TwinUpdateHistories)
        var twinHistoryCount = await _dbContext.TwinUpdateHistories
            .CountAsync(h => h.CenterId == _centerId && h.StudentId == _studentId);
        Assert.Equal(0, twinHistoryCount);
    }

    // 9. Client giả mạo Passed bị từ chối
    [Fact]
    public async Task Scenario9_ClientForgedPassed_IsRejected()
    {
        // Arrange: Incomplete session
        var tenantContext = new TenantContext();
        tenantContext.Initialize(_centerId, _studentId, "Student", 1);

        var pathId = Guid.NewGuid();
        var sessionGuid = Guid.NewGuid();
        var sessionDto = new LearningPathSessionDto
        {
            SessionId = sessionGuid.ToString(),
            TopicNodeId = "555",
            Title = "Forged Session",
            Status = "InProgress",
            StartedAtUtc = _now,
            RecommendedQuestionIds = ["1101", "1102"],
            RequiredCorrectRate = 0.80m
        };

        var weekDto = new LearningPathWeekDto
        {
            WeekNumber = 1,
            Sessions = [sessionDto],
            Checkpoint = new LearningPathCheckpointDto()
        };

        var phaseDto = new LearningPathPhaseDto
        {
            PhaseNumber = 1,
            Weeks = [weekDto]
        };

        var learningPath = new LearningPath
        {
            CenterId = _centerId,
            LearningPathId = pathId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            Strategy = LearningPathStrategy.OpportunityGap,
            Status = LearningPathStatus.Active,
            PlanJson = JsonSerializer.SerializeToDocument(new[] { phaseDto }),
            GeneratedAt = _now,
            CreatedAt = _now,
            UpdatedAt = _now,
            RowVersion = 1ul
        };
        await _dbContext.LearningPaths.AddAsync(learningPath);
        await SeedQuestionsAsync([1101, 1102], topicId: 555);
        await _dbContext.SaveChangesAsync();

        var updateUseCase = new UpdateLearningPathSessionUseCase(_dbContext, tenantContext, _service);

        // Act: Client attempts to send request.Status = "Completed" when no attempts were submitted
        var request = new UpdateLearningPathSessionRequest
        {
            Status = "Completed"
        };
        var result = await updateUseCase.ExecuteAsync(_subjectId, sessionGuid.ToString(), request, CancellationToken.None);

        // Assert: Must be rejected (false)
        Assert.False(result);

        // Session status remains InProgress
        var reloaded = await _dbContext.LearningPaths.SingleAsync(p => p.LearningPathId == pathId);
        var planPhases = JsonSerializer.Deserialize<List<LearningPathPhaseDto>>(reloaded.PlanJson!.RootElement.GetRawText())!;
        Assert.Equal("InProgress", planPhases[0].Weeks[0].Sessions[0].Status);
    }

    // 10. Một Attempt thuộc hai ngữ cảnh không tạo hai evidence
    [Fact]
    public async Task Scenario10_AttemptScopedToSessionRun_PreventsCrossContextDuplication()
    {
        // Arrange
        var practiceIds = new List<ulong> { 1201 };
        var checkpointIds = new List<ulong> { 1202 };
        var session = await CreateSessionAsync(practiceIds, checkpointIds);

        var run = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);

        // Create an attempt linked to SessionRunId
        var clientSubId = Guid.NewGuid();
        var attempt = CreateAttempt(run.SessionRunId, 1201, isCorrect: true);
        attempt.ClientSubmissionId = clientSubId;
        attempt.AssignmentId = null; // Strictly session context
        await _dbContext.Attempts.AddAsync(attempt);
        await _dbContext.SaveChangesAsync();

        // Verify only 1 attempt exists for this ClientSubmissionId
        var count = await _dbContext.Attempts.CountAsync(a => a.ClientSubmissionId == clientSubId);
        Assert.Equal(1, count);

        // And attempt has exact SessionRunId FK, not cross-referenced to assignment
        var savedAttempt = await _dbContext.Attempts.SingleAsync(a => a.ClientSubmissionId == clientSubId);
        Assert.Equal(run.SessionRunId, savedAttempt.SessionRunId);
        Assert.Null(savedAttempt.AssignmentId);
    }

    // 11. Chạy test trạng thái trong điều kiện cạnh tranh
    [Fact]
    public async Task Scenario11_Concurrency_InvalidTransitionsAndStaleUpdates_HandledSafely()
    {
        // Arrange: Terminal states cannot be transitioned back
        Assert.Throws<SessionStateTransitionException>(() =>
            LearningSessionExecutionService.ValidateRunTransition(SessionRunStatus.Completed, SessionRunStatus.InProgress));

        Assert.Throws<SessionStateTransitionException>(() =>
            LearningSessionExecutionService.ValidateRunTransition(SessionRunStatus.Abandoned, SessionRunStatus.Grading));

        Assert.Throws<SessionStateTransitionException>(() =>
            LearningSessionExecutionService.ValidateSessionTransition(LearningPathSessionStatus.Completed, LearningPathSessionStatus.InProgress));

        // Concurrency test on session run transition
        var session = await CreateSessionAsync(practiceIds: [1301], checkpointIds: [1302]);
        var run = await _service.StartOrResumeRunAsync(_centerId, _studentId, session.SessionId);

        // Transition to Grading
        var gradingRun = await _service.TransitionRunAsync(_centerId, run.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Grading,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Processing));
        Assert.Equal(SessionRunStatus.Grading, gradingRun.Status);

        // Once completed, cannot transition to InProgress
        await _service.TransitionRunAsync(_centerId, run.SessionRunId, new TransitionSessionRunRequest(
            TargetStatus: SessionRunStatus.Completed,
            TargetEvaluationStatus: SessionRunEvaluationStatus.Completed,
            TargetOutcome: SessionRunOutcome.Passed));

        await Assert.ThrowsAsync<SessionStateTransitionException>(() =>
            _service.TransitionRunAsync(_centerId, run.SessionRunId, new TransitionSessionRunRequest(
                TargetStatus: SessionRunStatus.InProgress)));
    }
}
