using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Update;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Override;

public sealed class TeacherOverrideReplayHardeningTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

    public TeacherOverrideReplayHardeningTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.Initialize(_centerId, _teacherId, nameof(UserRole.Teacher), 1);

        _dbContext = new EduTwinDbContext(options, _tenantContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private void SeedBaseHierarchy(Guid classId)
    {
        _dbContext.Centers.Add(new Center
        {
            CenterId = _centerId,
            CenterCode = $"C-{_centerId:N}"[..10],
            CenterName = "Test Center",
            Status = CenterStatus.Active,
            Timezone = "UTC",
            CreatedAt = _utcNow.AddDays(-1),
            UpdatedAt = _utcNow.AddDays(-1)
        });

        _dbContext.Teachers.Add(new Teacher
        {
            CenterId = _centerId,
            TeacherId = _teacherId,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        _dbContext.Students.Add(new Student
        {
            CenterId = _centerId,
            StudentId = _studentId,
            FullName = "Hardening Test Student",
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        _dbContext.Classes.Add(new Class
        {
            CenterId = _centerId,
            ClassId = classId,
            TeacherId = _teacherId,
            ClassName = "Class 11A",
            AcademicYear = "2026-2027",
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        _dbContext.ClassStudents.Add(new ClassStudent
        {
            CenterId = _centerId,
            ClassId = classId,
            StudentId = _studentId,
            Status = ClassStudentStatus.Active,
            JoinedAt = _utcNow
        });
    }

    private void SeedQuestions()
    {
        _dbContext.Questions.Add(new Question
        {
            CenterId = _centerId,
            QuestionId = 601,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 201,
            QuestionText = "Question 1",
            CorrectAnswer = "A",
            Solution = "Sol 1",
            Difficulty = 3,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        _dbContext.Questions.Add(new Question
        {
            CenterId = _centerId,
            QuestionId = 602,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 201,
            QuestionText = "Question 2 (Essay)",
            CorrectAnswer = "B",
            Solution = "Sol 2",
            Difficulty = 4,
            EstimatedTimeSeconds = 120,
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });
    }

    [Fact]
    public async Task Replay_StrictlyRespectsPersistedEvidenceWeight_IgnoresRawAnalysisConfidence()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        // Attempt 1: Question 601, had AnalysisConfidence = 95m, BUT EvidenceGate marked it ReviewOnly with ReasoningWeight = 0.00m
        var attempt1 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3001,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            ReasoningText = "Reasoning 1",
            IsCorrect = true,
            TimeSpentSeconds = 50,
            Confidence = 90m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = _utcNow.AddMinutes(-30),
            UpdatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.Attempts.Add(attempt1);

        var analysis1 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4001,
            AttemptId = 3001,
            ReasoningQuality = 95m,
            AnalysisConfidence = 95m, // High confidence, but gate gave 0 weight
            Feedback = "Rule violation in gate",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-30),
            UpdatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.ReasoningAnalyses.Add(analysis1);

        var evidence1 = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5001,
            AttemptId = 3001,
            AnalysisId = 4001,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.ReviewOnly,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 0.00m, // Persisted weight is 0.00!
            ReasonCodes = JsonDocument.Parse("[\"GATE_REJECTED\"]"),
            RequiresTeacherReview = true,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow.AddMinutes(-30),
            CreatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.EvidenceAssessments.Add(evidence1);

        // Attempt 2: Question 602, needing teacher review
        var attempt2 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3002,
            StudentId = _studentId,
            QuestionId = 602,
            FinalAnswer = "B",
            ReasoningText = "Reasoning 2",
            IsCorrect = false,
            TimeSpentSeconds = 80,
            Confidence = 85m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.Attempts.Add(attempt2);

        var analysis2 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4002,
            AttemptId = 3002,
            ReasoningQuality = 40m,
            AnalysisConfidence = 40m,
            Feedback = "Needs human review",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.ReasoningAnalyses.Add(analysis2);

        var evidence2 = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5002,
            AttemptId = 3002,
            AnalysisId = 4002,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.ReviewOnly,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 0.00m,
            ReasonCodes = JsonDocument.Parse("[\"AI_CONFIDENCE_BELOW_50\"]"),
            RequiresTeacherReview = true,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow.AddMinutes(-10),
            CreatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.EvidenceAssessments.Add(evidence2);

        // Attempt 3 is later than the overridden attempt and already has effective evidence.
        // Replay provenance must therefore point at this attempt, not at the override target.
        var attempt3 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3003,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            ReasoningText = "Later reasoning",
            IsCorrect = true,
            TimeSpentSeconds = 60,
            Confidence = 75m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = _utcNow.AddMinutes(-5),
            UpdatedAt = _utcNow.AddMinutes(-5)
        };
        _dbContext.Attempts.Add(attempt3);

        var analysis3 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4003,
            AttemptId = 3003,
            ReasoningQuality = 70m,
            AnalysisConfidence = 75m,
            Feedback = "Later effective analysis",
            IsFallback = false,
            NeedsTeacherReview = false,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-5),
            UpdatedAt = _utcNow.AddMinutes(-5)
        };
        _dbContext.ReasoningAnalyses.Add(analysis3);

        _dbContext.EvidenceAssessments.Add(new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5003,
            AttemptId = 3003,
            AnalysisId = 4003,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.Reduced,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 0.50m,
            ReasonCodes = JsonDocument.Parse("[\"AI_CONFIDENCE_50_TO_79\"]"),
            RequiresTeacherReview = false,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow.AddMinutes(-5),
            CreatedAt = _utcNow.AddMinutes(-5)
        });

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            ErrorType = ErrorType.None,
            Feedback = "Teacher verified correctness.",
            IsCorrect = true,
            Reason = "Direct review",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(4002, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);
        Assert.NotNull(result.Data);

        // All three attempts in the topic were replayed chronologically.
        Assert.Equal(3, result.Data.Replay.AttemptsReplayed);

        // Attempt 1 has zero weight; the override and later attempt are effective.
        var twin = await _dbContext.KnowledgeTwins.SingleAsync(k => k.CenterId == _centerId && k.StudentId == _studentId && k.TopicNodeId == 201);
        Assert.Equal(2u, twin.EvidenceCount);
        Assert.Equal(3003ul, twin.LastAttemptId);
        Assert.Equal(70m, twin.LastReasoningQuality);

        // RecommendationRecalculated must be false
        Assert.False(result.Data.Replay.RecommendationRecalculated);

        // Check calculation breakdown in history
        var history = await _dbContext.TwinUpdateHistories
            .SingleAsync(h => h.CenterId == _centerId && h.StudentId == _studentId && h.EventSource == TwinEventSource.Replay);
        var doc = JsonDocument.Parse(history.CalculationBreakdown.RootElement.GetRawText());
        Assert.Equal("replay-v1", history.CalculationVersion);
        Assert.Equal(70m, history.EffectiveReasoningQuality);
        Assert.Equal(3002ul, doc.RootElement.GetProperty("TriggerAttemptId").GetUInt64());
        Assert.Equal(3, doc.RootElement.GetProperty("ReplayCount").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("EffectiveEvidenceCount").GetInt32());
        Assert.False(doc.RootElement.TryGetProperty("ReasoningWeight", out _));

        var replaySteps = doc.RootElement.GetProperty("ReplaySteps").EnumerateArray().ToList();
        Assert.Equal(3, replaySteps.Count);
        var finalStep = replaySteps[^1];
        Assert.Equal(3003ul, finalStep.GetProperty("AttemptId").GetUInt64());
        Assert.Equal(70m, finalStep.GetProperty("EffectiveReasoningQuality").GetDecimal());
        Assert.Equal(0.50m, finalStep.GetProperty("ReasoningWeight").GetDecimal());
        Assert.True(finalStep.TryGetProperty("TimeQuality", out _));
        Assert.True(finalStep.TryGetProperty("DifficultyMultiplier", out _));
        Assert.True(finalStep.TryGetProperty("LearningRate", out _));
    }

    [Fact]
    public async Task Override_UpdatesBehaviorTwinCalibration_WhenEssayOrNullCorrectnessOverridden()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        // Attempt 1: Confidence 80, IsCorrect = true -> calibration: 100 - |80 - 100| = 80
        var attempt1 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3101,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            ReasoningText = "Attempt 1",
            IsCorrect = true,
            TimeSpentSeconds = 60,
            Confidence = 80m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = _utcNow.AddMinutes(-20),
            UpdatedAt = _utcNow.AddMinutes(-20)
        };
        _dbContext.Attempts.Add(attempt1);

        // Attempt 2 (Essay): Confidence 70, IsCorrect = null initially
        var attempt2 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3102,
            StudentId = _studentId,
            QuestionId = 602,
            FinalAnswer = "B",
            ReasoningText = "Essay reasoning",
            IsCorrect = null,
            TimeSpentSeconds = 120,
            Confidence = 70m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.Attempts.Add(attempt2);

        var analysis2 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4102,
            AttemptId = 3102,
            ReasoningQuality = 50m,
            AnalysisConfidence = 50m,
            Feedback = "Essay pending grading",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.ReasoningAnalyses.Add(analysis2);

        // Seed initial BehaviorTwin with calibration = 80.00 (only attempt 1 was counted)
        var behaviorTwin = new BehaviorTwin
        {
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            ConfidenceCalibration = 80.00m,
            AvgConfidence = 80m,
            AvgTimeSpentSeconds = 60m,
            AttemptCount = 1,
            CreatedAt = _utcNow.AddMinutes(-20),
            UpdatedAt = _utcNow.AddMinutes(-20)
        };
        _dbContext.BehaviorTwins.Add(behaviorTwin);

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            ErrorType = ErrorType.None,
            Feedback = "Essay graded as correct.",
            IsCorrect = true,
            Reason = "Graded by teacher",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(4102, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);

        // Check calibration:
        // Attempt 1: 100 - |80 - 100| = 80
        // Attempt 2: 100 - |70 - 100| = 70
        // Average: (80 + 70) / 2 = 75.00m
        var updatedBehaviorTwin = await _dbContext.BehaviorTwins.SingleAsync(b => b.CenterId == _centerId && b.StudentId == _studentId && b.SubjectId == _subjectId);
        Assert.Equal(75.00m, updatedBehaviorTwin.ConfidenceCalibration);
    }

    [Fact]
    public async Task Replay_UpdatesEvidenceCount_OnPreExistingKnowledgeTwin()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        // Seed pre-existing KnowledgeTwin with outdated EvidenceCount = 42
        var existingTwin = new KnowledgeTwin
        {
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 201,
            MasteryPercentage = 50m,
            EvidenceCount = 42,
            CreatedAt = _utcNow.AddDays(-1),
            UpdatedAt = _utcNow.AddDays(-1)
        };
        _dbContext.KnowledgeTwins.Add(existingTwin);

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3201,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            ReasoningText = "Attempt",
            IsCorrect = false,
            TimeSpentSeconds = 60,
            Confidence = 80m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-5),
            UpdatedAt = _utcNow.AddMinutes(-5)
        };
        _dbContext.Attempts.Add(attempt);

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4201,
            AttemptId = 3201,
            ReasoningQuality = 45m,
            AnalysisConfidence = 45m,
            Feedback = "Needs review",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-5),
            UpdatedAt = _utcNow.AddMinutes(-5)
        };
        _dbContext.ReasoningAnalyses.Add(analysis);

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            ErrorType = ErrorType.None,
            IsCorrect = true,
            Reason = "Fixed",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(4201, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);

        // Verification: EvidenceCount must be updated to 1 (the 1 positive weight attempt), NOT left at 42!
        var twin = await _dbContext.KnowledgeTwins.SingleAsync(k => k.CenterId == _centerId && k.StudentId == _studentId && k.TopicNodeId == 201);
        Assert.Equal(1u, twin.EvidenceCount);
    }

    [Fact]
    public async Task ExecuteAsync_DbUpdateConcurrencyException_ReturnsConflict()
    {
        var interceptor = new ThrowingConcurrencyInterceptor();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new EduTwinDbContext(options, _tenantContext);

        var classId = Guid.NewGuid();
        dbContext.Centers.Add(new Center
        {
            CenterId = _centerId,
            CenterCode = "C-CONCURR",
            CenterName = "Concurrency Center",
            Status = CenterStatus.Active,
            Timezone = "UTC",
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });
        dbContext.Teachers.Add(new Teacher { CenterId = _centerId, TeacherId = _teacherId, CreatedAt = _utcNow, UpdatedAt = _utcNow });
        dbContext.Students.Add(new Student { CenterId = _centerId, StudentId = _studentId, FullName = "Concurrency Student", CreatedAt = _utcNow, UpdatedAt = _utcNow });
        dbContext.Classes.Add(new Class { CenterId = _centerId, ClassId = classId, TeacherId = _teacherId, ClassName = "Class Concurr", AcademicYear = "2026-2027", CreatedAt = _utcNow, UpdatedAt = _utcNow });
        dbContext.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = classId, StudentId = _studentId, Status = ClassStudentStatus.Active, JoinedAt = _utcNow });

        dbContext.Questions.Add(new Question
        {
            CenterId = _centerId,
            QuestionId = 603,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 201,
            QuestionText = "Question 3",
            CorrectAnswer = "A",
            Solution = "Sol 3",
            Difficulty = 3,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3301,
            StudentId = _studentId,
            QuestionId = 603,
            FinalAnswer = "A",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        dbContext.Attempts.Add(attempt);

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4301,
            AttemptId = 3301,
            ReasoningQuality = 40m,
            AnalysisConfidence = 40m,
            Feedback = "Needs review",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        dbContext.ReasoningAnalyses.Add(analysis);

        await dbContext.SaveChangesAsync();

        // Enable concurrency exception simulation on next save
        interceptor.ShouldThrow = true;

        var useCase = new TeacherOverrideUseCase(
            dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(dbContext),
            new StudentTwinUpdater(dbContext),
            new TwinUpdateHistoryWriter(dbContext),
            TimeProvider.System);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            ErrorType = ErrorType.None,
            IsCorrect = true,
            Reason = "Concurrent override",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(4301, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_OverrideScore_SetsOverrideAwardedScore_DoesNotMutateAttemptAwardedScoreOrIsCorrect()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3401,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            ReasoningText = "Preliminary work",
            IsCorrect = false,
            AwardedScore = 3.00m,
            TimeSpentSeconds = 50,
            Confidence = 85m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-5),
            UpdatedAt = _utcNow.AddMinutes(-5)
        };
        _dbContext.Attempts.Add(attempt);

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4401,
            AttemptId = 3401,
            ReasoningQuality = 40m,
            AnalysisConfidence = 40m,
            Feedback = "Needs teacher check",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-5),
            UpdatedAt = _utcNow.AddMinutes(-5)
        };
        _dbContext.ReasoningAnalyses.Add(analysis);

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 85m,
            ErrorType = ErrorType.None,
            Feedback = "Teacher verified answer and awarded partial credit.",
            IsCorrect = true,
            AwardedScore = 8.50m,
            Reason = "Manual scoring",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(4401, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(8.50m, result.Data.OverrideAwardedScore);
        Assert.Equal(8.50m, result.Data.EffectiveAwardedScore);

        // Verification: Attempt preliminary provenance is strictly preserved!
        var savedAttempt = await _dbContext.Attempts.SingleAsync(a => a.AttemptId == 3401);
        Assert.False(savedAttempt.IsCorrect);
        Assert.Equal(3.00m, savedAttempt.AwardedScore);

        // Verification: Override values are recorded on ReasoningAnalysis
        var savedAnalysis = await _dbContext.ReasoningAnalyses.SingleAsync(a => a.AnalysisId == 4401);
        Assert.True(savedAnalysis.OverrideIsCorrect);
        Assert.Equal(8.50m, savedAnalysis.OverrideAwardedScore);
        Assert.Equal(1u, savedAnalysis.OverrideVersion);
    }

    [Fact]
    public async Task ExecuteAsync_OverrideScoreNull_ResetsToPreliminaryScore()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3402,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            IsCorrect = false,
            AwardedScore = 4.00m,
            TimeSpentSeconds = 50,
            Confidence = 85m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-5),
            UpdatedAt = _utcNow.AddMinutes(-5)
        };
        _dbContext.Attempts.Add(attempt);

        // Analysis starts already overridden once with score 9.00m, version 1
        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4402,
            AttemptId = 3402,
            ReasoningQuality = 40m,
            AnalysisConfidence = 40m,
            OverrideAwardedScore = 9.00m,
            OverrideIsCorrect = true,
            Feedback = "First override",
            IsFallback = false,
            NeedsTeacherReview = false,
            OverrideVersion = 1,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-5),
            UpdatedAt = _utcNow.AddMinutes(-5)
        };
        _dbContext.ReasoningAnalyses.Add(analysis);

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        // Teacher sends AwardedScore = null to clear override and reset to preliminary
        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 85m,
            ErrorType = ErrorType.None,
            Feedback = "Resetting override score to preliminary",
            IsCorrect = false,
            AwardedScore = null,
            Reason = "Reset override score",
            OverrideVersion = 1
        };

        var result = await useCase.ExecuteAsync(4402, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.OverrideAwardedScore);
        // EffectiveAwardedScore falls back to preliminary 4.00m
        Assert.Equal(4.00m, result.Data.EffectiveAwardedScore);

        var savedAnalysis = await _dbContext.ReasoningAnalyses.SingleAsync(a => a.AnalysisId == 4402);
        Assert.Null(savedAnalysis.OverrideAwardedScore);
        Assert.Equal(2u, savedAnalysis.OverrideVersion);

        var savedAttempt = await _dbContext.Attempts.SingleAsync(a => a.AttemptId == 3402);
        Assert.Equal(4.00m, savedAttempt.AwardedScore);
    }

    [Fact]
    public async Task ExecuteAsync_AssignmentAttempt_UpdatesStudentAssignmentProgress()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        var assignmentId = Guid.NewGuid();
        var progress = new StudentAssignmentProgress
        {
            ProgressId = 7001,
            CenterId = _centerId,
            AssignmentId = assignmentId,
            StudentId = _studentId,
            Status = ProgressStatus.NotStarted,
            CompletedQuestionCount = 0,
            TotalQuestionCount = 2,
            CreatedAt = _utcNow.AddHours(-1),
            UpdatedAt = _utcNow.AddHours(-1)
        };
        _dbContext.StudentAssignmentProgresses.Add(progress);

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3403,
            StudentId = _studentId,
            AssignmentId = assignmentId,
            QuestionId = 601,
            FinalAnswer = "A",
            IsCorrect = false,
            TimeSpentSeconds = 60,
            Confidence = 80m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.Attempts.Add(attempt);

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4403,
            AttemptId = 3403,
            ReasoningQuality = 40m,
            AnalysisConfidence = 40m,
            Feedback = "Needs review",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.ReasoningAnalyses.Add(analysis);

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            ErrorType = ErrorType.None,
            Feedback = "Overridden to correct",
            IsCorrect = true,
            Reason = "Assignment check",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(4403, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);

        var updatedProgress = await _dbContext.StudentAssignmentProgresses.SingleAsync(p => p.ProgressId == 7001);
        Assert.Equal(1u, updatedProgress.CompletedQuestionCount);
        Assert.Equal(ProgressStatus.InProgress, updatedProgress.Status);
        Assert.NotNull(updatedProgress.StartedAt);
    }

    [Fact]
    public async Task Replay_FailClosed_WhenPositiveWeightEvidenceHasNullCorrectness_ThrowsInvalidOperationException()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        // Attempt 1 with positive reasoning weight (0.80m) but null correctness
        var attempt1 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3404,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            IsCorrect = null, // UNRESOLVED!
            TimeSpentSeconds = 60,
            Confidence = 80m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = _utcNow.AddMinutes(-20),
            UpdatedAt = _utcNow.AddMinutes(-20)
        };
        _dbContext.Attempts.Add(attempt1);

        var analysis1 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4404,
            AttemptId = 3404,
            ReasoningQuality = 80m,
            AnalysisConfidence = 80m,
            OverrideIsCorrect = null, // ALSO UNRESOLVED!
            Feedback = "Initial analysis",
            IsFallback = false,
            NeedsTeacherReview = false,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-20),
            UpdatedAt = _utcNow.AddMinutes(-20)
        };
        _dbContext.ReasoningAnalyses.Add(analysis1);

        var evidence1 = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5404,
            AttemptId = 3404,
            AnalysisId = 4404,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.Trusted,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 0.80m, // Positive weight with null correctness!
            ReasonCodes = JsonDocument.Parse("[]"),
            RequiresTeacherReview = false,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow.AddMinutes(-20),
            CreatedAt = _utcNow.AddMinutes(-20)
        };
        _dbContext.EvidenceAssessments.Add(evidence1);

        // Attempt 2 that teacher is trying to override
        var attempt2 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3405,
            StudentId = _studentId,
            QuestionId = 602,
            FinalAnswer = "B",
            IsCorrect = false,
            TimeSpentSeconds = 60,
            Confidence = 80m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.Attempts.Add(attempt2);

        var analysis2 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4405,
            AttemptId = 3405,
            ReasoningQuality = 40m,
            AnalysisConfidence = 40m,
            Feedback = "Needs review",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.ReasoningAnalyses.Add(analysis2);

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            ErrorType = ErrorType.None,
            Feedback = "Teacher override",
            IsCorrect = true,
            Reason = "Reviewing attempt 2",
            OverrideVersion = 0
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(4405, request, CancellationToken.None));
        Assert.Contains("unresolved correctness", ex.Message);
    }

    [Fact]
    public async Task Replay_SubjectWideRollingCalibration_TotalOrderingPreventsFutureLeakage()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        // Question 603 in Topic 202
        _dbContext.Questions.Add(new Question
        {
            CenterId = _centerId,
            QuestionId = 603,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 202,
            QuestionText = "Topic 202 Question",
            CorrectAnswer = "C",
            Solution = "Sol 3",
            Difficulty = 3,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        // Attempt 1: Topic 201, T1 = -30m, confidence = 90, IsCorrect = true -> calibration = 90
        var attempt1 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3501,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            IsCorrect = true,
            TimeSpentSeconds = 60,
            Confidence = 90m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = _utcNow.AddMinutes(-30),
            UpdatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.Attempts.Add(attempt1);

        var analysis1 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4501,
            AttemptId = 3501,
            ReasoningQuality = 90m,
            AnalysisConfidence = 90m,
            Feedback = "Good",
            IsFallback = false,
            NeedsTeacherReview = false,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-30),
            UpdatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.ReasoningAnalyses.Add(analysis1);

        var evidence1 = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5501,
            AttemptId = 3501,
            AnalysisId = 4501,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.Trusted,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 1.00m,
            ReasonCodes = JsonDocument.Parse("[]"),
            RequiresTeacherReview = false,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow.AddMinutes(-30),
            CreatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.EvidenceAssessments.Add(evidence1);

        // Attempt 2: Topic 202, T2 = -20m, confidence = 50, IsCorrect = false -> calibration = 100 - |50 - 0| = 50
        var attempt2 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3502,
            StudentId = _studentId,
            QuestionId = 603,
            FinalAnswer = "X",
            IsCorrect = false,
            TimeSpentSeconds = 60,
            Confidence = 50m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = _utcNow.AddMinutes(-20),
            UpdatedAt = _utcNow.AddMinutes(-20)
        };
        _dbContext.Attempts.Add(attempt2);

        var analysis2 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4502,
            AttemptId = 3502,
            ReasoningQuality = 50m,
            AnalysisConfidence = 50m,
            Feedback = "Topic 202",
            IsFallback = false,
            NeedsTeacherReview = false,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-20),
            UpdatedAt = _utcNow.AddMinutes(-20)
        };
        _dbContext.ReasoningAnalyses.Add(analysis2);

        var evidence2 = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5502,
            AttemptId = 3502,
            AnalysisId = 4502,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.Trusted,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 1.00m,
            ReasonCodes = JsonDocument.Parse("[]"),
            RequiresTeacherReview = false,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow.AddMinutes(-20),
            CreatedAt = _utcNow.AddMinutes(-20)
        };
        _dbContext.EvidenceAssessments.Add(evidence2);

        // Attempt 3: Topic 201, T3 = -10m, confidence = 100, IsCorrect = true -> calibration = 100
        var attempt3 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3503,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            IsCorrect = false,
            TimeSpentSeconds = 60,
            Confidence = 100m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.Attempts.Add(attempt3);

        var analysis3 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4503,
            AttemptId = 3503,
            ReasoningQuality = 40m,
            AnalysisConfidence = 40m,
            Feedback = "Needs review",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.ReasoningAnalyses.Add(analysis3);

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 95m,
            ErrorType = ErrorType.None,
            Feedback = "Overriding attempt 3 to correct",
            IsCorrect = true,
            Reason = "Replay test",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(4503, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);

        // Verify TwinUpdateHistory has ReplaySteps with rolling calibration
        var history = await _dbContext.TwinUpdateHistories
            .SingleAsync(h => h.CenterId == _centerId && h.StudentId == _studentId && h.EventSource == TwinEventSource.Replay);

        var breakdownJson = history.CalculationBreakdown.RootElement;
        Assert.True(breakdownJson.TryGetProperty("ReplaySteps", out var replayStepsElement));
        var steps = replayStepsElement.EnumerateArray().ToList();
        Assert.Equal(2, steps.Count);

        // Step 1: Attempt 1 at T1 (-30m)
        // Rolling calibration evaluated only on attempts up to T1 in the subject -> only Attempt 1!
        // Calibration = 100 - |90 - 100| = 90.00m (Attempt 2 at -20m and Attempt 3 at -10m do NOT leak!)
        var step1 = steps[0];
        Assert.Equal(3501ul, step1.GetProperty("AttemptId").GetUInt64());
        Assert.Equal(90.00m, step1.GetProperty("RollingCalibration").GetDecimal());

        // Step 2: Attempt 3 at T3 (-10m)
        // Rolling calibration evaluated on attempts up to T3 -> Attempt 1 (90), Attempt 2 (50), Attempt 3 (100)
        // Mean = (90 + 50 + 100) / 3 = 80.00m!
        var step2 = steps[1];
        Assert.Equal(3503ul, step2.GetProperty("AttemptId").GetUInt64());
        Assert.Equal(80.00m, step2.GetProperty("RollingCalibration").GetDecimal());
    }

    [Fact]
    public async Task Replay_ReviewOnlyNullCorrectness_PreservesNullInHistory()
    {
        var classId = Guid.NewGuid();
        SeedBaseHierarchy(classId);
        SeedQuestions();

        // Attempt 1: Essay on Topic 101, T1 = -30m, IsCorrect = null, ReviewOnly with ReasoningWeight = 0
        var attempt1 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3601,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "Essay content",
            IsCorrect = null,
            AwardedScore = null,
            TimeSpentSeconds = 60,
            Confidence = 80m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-30),
            UpdatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.Attempts.Add(attempt1);

        var analysis1 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4601,
            AttemptId = 3601,
            ReasoningQuality = null,
            AnalysisConfidence = 50m,
            Feedback = "Pending human review",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-30),
            UpdatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.ReasoningAnalyses.Add(analysis1);

        var evidence1 = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5601,
            AttemptId = 3601,
            AnalysisId = 4601,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.ReviewOnly,
            DecisionMode = EvidenceDecisionMode.DeterministicOnly,
            ReasoningWeight = 0.00m,
            ReasonCodes = JsonDocument.Parse("[\"PENDING_ESSAY_REVIEW\"]"),
            RequiresTeacherReview = true,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow.AddMinutes(-30),
            CreatedAt = _utcNow.AddMinutes(-30)
        };
        _dbContext.EvidenceAssessments.Add(evidence1);

        // Attempt 2: Topic 101, T2 = -10m, preliminary false, ReviewOnly, overridden by teacher
        var attempt2 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3602,
            StudentId = _studentId,
            QuestionId = 601,
            FinalAnswer = "A",
            IsCorrect = false,
            AwardedScore = 0m,
            TimeSpentSeconds = 60,
            Confidence = 90m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.Attempts.Add(attempt2);

        var analysis2 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4602,
            AttemptId = 3602,
            ReasoningQuality = 40m,
            AnalysisConfidence = 45m,
            Feedback = "Low confidence",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow.AddMinutes(-10),
            UpdatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.ReasoningAnalyses.Add(analysis2);

        var evidence2 = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5602,
            AttemptId = 3602,
            AnalysisId = 4602,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.ReviewOnly,
            DecisionMode = EvidenceDecisionMode.DeterministicOnly,
            ReasoningWeight = 0.00m,
            ReasonCodes = JsonDocument.Parse("[\"LOW_CONFIDENCE\"]"),
            RequiresTeacherReview = true,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow.AddMinutes(-10),
            CreatedAt = _utcNow.AddMinutes(-10)
        };
        _dbContext.EvidenceAssessments.Add(evidence2);

        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow);
        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 85m,
            ErrorType = ErrorType.None,
            Feedback = "Confirmed correct by teacher",
            IsCorrect = true,
            AwardedScore = 10m,
            Reason = "Essay replay preservation test",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(4602, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);

        // Verify TwinUpdateHistory has ReplaySteps with preserved null correctness
        var history = await _dbContext.TwinUpdateHistories
            .SingleAsync(h => h.CenterId == _centerId && h.StudentId == _studentId && h.EventSource == TwinEventSource.Replay);

        var breakdownJson = history.CalculationBreakdown.RootElement;
        Assert.True(breakdownJson.TryGetProperty("ReplaySteps", out var replayStepsElement));
        var steps = replayStepsElement.EnumerateArray().ToList();
        Assert.Equal(2, steps.Count);

        // Step 1: Historical Essay with IsCorrect = null, ReasoningWeight = 0.00m
        var essayStep = steps[0];
        Assert.Equal(3601ul, essayStep.GetProperty("AttemptId").GetUInt64());
        Assert.Equal(JsonValueKind.Null, essayStep.GetProperty("EffectiveCorrectness").ValueKind);
        Assert.Equal(0.00m, essayStep.GetProperty("ReasoningWeight").GetDecimal());
        Assert.Equal(0.00m, essayStep.GetProperty("Delta").GetDecimal());

        // Step 2: Overridden Attempt with IsCorrect = true, ReasoningWeight = 1.00m
        var overriddenStep = steps[1];
        Assert.Equal(3602ul, overriddenStep.GetProperty("AttemptId").GetUInt64());
        Assert.True(overriddenStep.GetProperty("EffectiveCorrectness").GetBoolean());
        Assert.Equal(1.00m, overriddenStep.GetProperty("ReasoningWeight").GetDecimal());
        Assert.True(overriddenStep.GetProperty("Delta").GetDecimal() > 0m);
    }

    private sealed class ThrowingConcurrencyInterceptor : SaveChangesInterceptor
    {
        public bool ShouldThrow { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict", Array.Empty<IUpdateEntry>());
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
