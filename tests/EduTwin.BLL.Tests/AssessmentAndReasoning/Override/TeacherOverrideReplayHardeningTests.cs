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
using EduTwin.DAL.AssessmentAndReasoning;
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

        // Attempts replayed count is 2 (both attempts in the topic were checked chronologically)
        Assert.Equal(2, result.Data.Replay.AttemptsReplayed);

        // Verification: EvidenceCount must be 1, because Attempt 1 had weight 0.00 and Attempt 2 had weight 1.00
        var twin = await _dbContext.KnowledgeTwins.SingleAsync(k => k.CenterId == _centerId && k.StudentId == _studentId && k.TopicNodeId == 201);
        Assert.Equal(1u, twin.EvidenceCount);

        // RecommendationRecalculated must be false
        Assert.False(result.Data.Replay.RecommendationRecalculated);

        // Check calculation breakdown in history
        var history = await _dbContext.TwinUpdateHistories
            .SingleAsync(h => h.CenterId == _centerId && h.StudentId == _studentId && h.EventSource == TwinEventSource.TeacherOverride);
        var doc = JsonDocument.Parse(history.CalculationBreakdown.RootElement.GetRawText());
        Assert.Equal(1.00m, doc.RootElement.GetProperty("ReasoningWeight").GetDecimal());
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
