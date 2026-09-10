using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Override;

public sealed class TeacherOverrideUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

    public TeacherOverrideUseCaseTests()
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

    private void SeedQuestion()
    {
        _dbContext.Questions.Add(new Question
        {
            CenterId = _centerId,
            QuestionId = 501,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 101,
            QuestionText = "Question 1",
            CorrectAnswer = "A",
            Solution = "A is correct",
            Difficulty = 3,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });
    }

    [Fact]
    public async Task ExecuteAsync_ValidOverride_ReplaysAttemptsChronologicallyAndReturnsReplaySummary()
    {
        // 1. Seed Teacher, Class, Student, ClassStudent
        var classId = Guid.NewGuid();
        _dbContext.Teachers.Add(new Teacher { CenterId = _centerId, TeacherId = _teacherId, CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.Students.Add(new Student { CenterId = _centerId, StudentId = _studentId, FullName = "Test Student", CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.Classes.Add(new Class { CenterId = _centerId, ClassId = classId, TeacherId = _teacherId, ClassName = "Class 10A", AcademicYear = "2026-2027", CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = classId, StudentId = _studentId, Status = ClassStudentStatus.Active, JoinedAt = _utcNow });

        // 2. Seed Question with Topic 101
        SeedQuestion();

        // 3. Seed Attempt with Analysis needing review
        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1001,
            StudentId = _studentId,
            QuestionId = 501,
            FinalAnswer = "A",
            ReasoningText = "My reasoning",
            IsCorrect = false,
            TimeSpentSeconds = 60,
            Confidence = 90m,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Attempts.Add(attempt);

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 2001,
            AttemptId = 1001,
            ReasoningQuality = 40m,
            AnalysisConfidence = 45m,
            Feedback = "Needs human review",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.ReasoningAnalyses.Add(analysis);

        var previousEvidence = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 3001,
            AttemptId = 1001,
            AnalysisId = 2001,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.ReviewOnly,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 0.00m,
            ReasonCodes = JsonDocument.Parse("[\"AI_CONFIDENCE_BELOW_50\"]"),
            RequiresTeacherReview = true,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = _utcNow,
            CreatedAt = _utcNow
        };
        _dbContext.EvidenceAssessments.Add(previousEvidence);
        await _dbContext.SaveChangesAsync();

        var timeProvider = new FixedTimeProvider(_utcNow.AddMinutes(5));
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
            ReasoningQuality = 88m,
            ErrorType = ErrorType.Presentation,
            Feedback = "Cách giải đúng, chỉ thiếu kết luận ngắn gọn.",
            IsCorrect = true,
            Reason = "Giáo viên đã kiểm tra lại bài trực tiếp.",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(2001, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("2001", result.Data.AnalysisId);
        Assert.True(result.Data.HasTeacherOverride);
        Assert.Equal(1u, result.Data.OverrideVersion);
        Assert.Equal(1, result.Data.Replay.AttemptsReplayed);
        Assert.True(result.Data.Replay.NewMastery > 0m);

        // Verify analysis was updated
        Assert.Equal(88m, analysis.OverrideReasoningQuality);
        Assert.True(analysis.OverrideIsCorrect);
        Assert.Equal(1u, analysis.OverrideVersion);
        Assert.False(analysis.NeedsTeacherReview);
        Assert.Equal(_teacherId, analysis.OverriddenByTeacherId);

        // Verify preliminary correctness provenance is preserved, and override is recorded in analysis
        Assert.False(attempt.IsCorrect);
        Assert.True(analysis.OverrideIsCorrect);
        Assert.Equal(true, analysis.OverrideIsCorrect ?? attempt.IsCorrect);

        // Verify new EvidenceAssessment was created with superseding
        var newEvidence = await _dbContext.EvidenceAssessments
            .SingleAsync(e => e.CenterId == _centerId && e.AnalysisOverrideVersion == 1);
        Assert.Equal(EvidenceSourceType.TeacherOverride, newEvidence.SourceType);
        Assert.Equal(EvidenceTrustLevel.Trusted, newEvidence.TrustLevel);
        Assert.Equal(1.00m, newEvidence.ReasoningWeight);
        Assert.Equal(3001u, newEvidence.SupersedesAssessmentId);

        // Verify KnowledgeTwin was updated
        var twin = await _dbContext.KnowledgeTwins.SingleAsync(k => k.CenterId == _centerId && k.StudentId == _studentId);
        Assert.True(twin.MasteryPercentage > 0m);
    }

    [Fact]
    public async Task ExecuteAsync_StaleOverrideVersion_ReturnsConflict()
    {
        var classId = Guid.NewGuid();
        _dbContext.Teachers.Add(new Teacher { CenterId = _centerId, TeacherId = _teacherId, CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.Students.Add(new Student { CenterId = _centerId, StudentId = _studentId, FullName = "Test Student", CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.Classes.Add(new Class { CenterId = _centerId, ClassId = classId, TeacherId = _teacherId, ClassName = "Class 10A", AcademicYear = "2026-2027", CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = classId, StudentId = _studentId, Status = ClassStudentStatus.Active, JoinedAt = _utcNow });

        SeedQuestion();

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 2002,
            AttemptId = 1002,
            OverrideVersion = 2,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            Feedback = "Test",
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.ReasoningAnalyses.Add(analysis);

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1002,
            StudentId = _studentId,
            QuestionId = 501,
            FinalAnswer = "A",
            ReasoningLanguage = "vi",
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Attempts.Add(attempt);
        await _dbContext.SaveChangesAsync();

        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            TimeProvider.System);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            IsCorrect = true,
            Reason = "Override",
            OverrideVersion = 1 // Stale! Current is 2
        };

        var result = await useCase.ExecuteAsync(2002, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_NonOwnerTeacher_ReturnsForbidden()
    {
        // Teacher is not teaching this student
        var otherTeacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        _dbContext.Teachers.Add(new Teacher { CenterId = _centerId, TeacherId = otherTeacherId, CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.Students.Add(new Student { CenterId = _centerId, StudentId = _studentId, FullName = "Other Student", CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.Classes.Add(new Class { CenterId = _centerId, ClassId = classId, TeacherId = otherTeacherId, ClassName = "Class 10B", AcademicYear = "2026-2027", CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = classId, StudentId = _studentId, Status = ClassStudentStatus.Active, JoinedAt = _utcNow });

        SeedQuestion();

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1003,
            StudentId = _studentId,
            QuestionId = 501,
            FinalAnswer = "A",
            ReasoningLanguage = "vi",
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Attempts.Add(attempt);

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 2003,
            AttemptId = 1003,
            OverrideVersion = 0,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            Feedback = "Test",
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.ReasoningAnalyses.Add(analysis);
        await _dbContext.SaveChangesAsync();

        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            TimeProvider.System);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            IsCorrect = true,
            Reason = "Override",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(2003, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Forbidden, result.Status);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
