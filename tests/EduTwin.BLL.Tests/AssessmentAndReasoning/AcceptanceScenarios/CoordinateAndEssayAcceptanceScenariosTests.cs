using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.DigitalTwin.Orchestration;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AcceptanceScenarios;

/// <summary>
/// Proves the 3 mandatory acceptance scenarios:
/// Scenario A: Coordinate2D equivalence (1, 1) vs (1,1) -> IsCorrect=true, full score, COORDINATE_EQUIVALENT
/// Scenario B: Essay preliminary null/null -> ReviewOnly, NeedsTeacherReview, score null, Mastery unchanged
/// Scenario C: Essay teacher override -> persists teacher provenance, creates effective grade, replay updates Mastery
/// </summary>
public sealed class CoordinateAndEssayAcceptanceScenariosTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly ulong _topicNodeId = 2001;
    private readonly DateTime _now = new(2026, 9, 26, 10, 0, 0, DateTimeKind.Utc);

    private readonly EvidenceGate _gate = new();
    private readonly EvidenceAssessmentFactory _evidenceFactory = new();
    private readonly EvidenceConsistencyChecker _consistencyChecker = new();
    private readonly ShortAnswerGrader _shortAnswerGrader = new();
    private readonly EssayGrader _essayGrader = new();
    private readonly AIReasoningAnalysisBuilder _analysisBuilder = new();
    private readonly RuleBasedFallbackBuilder _fallbackBuilder = new();

    public CoordinateAndEssayAcceptanceScenariosTests()
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

    private TwinCompletionOrchestrator CreateOrchestrator()
    {
        return new TwinCompletionOrchestrator(
            _dbContext,
            _gate,
            _evidenceFactory,
            new BehaviorTwinUpdater(_dbContext),
            new KnowledgeTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            _consistencyChecker);
    }

    // =========================================================================
    // SCENARIO A: Structured Coordinate Matching
    // =========================================================================
    [Theory]
    [InlineData("(1,1)", "(1, 1)")]
    [InlineData("(1;1)", "(1, 1)")]
    [InlineData("( 1 ; 1 )", "(1, 1)")]
    [InlineData("\\left(1,1\\right)", "(1, 1)")]
    [InlineData("I(1;1)", "(1, 1)")]
    [InlineData("(1/2; 2/4)", "(0.5; 0.5)")]
    [InlineData("(0.5; 0.5)", "(1/2; 1/2)")]
    public void ScenarioA_Coordinate2D_EquivalentCoordinates_ReturnsCorrectWithFullScoreAndReason(
        string studentInput,
        string referenceAnswer)
    {
        // Arrange
        const decimal maxScore = 40m;
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.Coordinate2D,
            MaxScore = maxScore
        };

        // Act
        var result = _shortAnswerGrader.Grade(studentInput, referenceAnswer, context);

        // Assert
        Assert.True(result.IsCorrect);
        Assert.Equal(maxScore, result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.CoordinateEquivalent, result.ReasonCode);
    }

    [Fact]
    public void ScenarioA_Coordinate2D_MismatchedCoordinates_ReturnsFalseWithZeroScore()
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.Coordinate2D,
            MaxScore = 40m
        };

        var result = _shortAnswerGrader.Grade("(1,2)", "(2,1)", context);

        Assert.False(result.IsCorrect);
        Assert.Equal(0m, result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.CoordinateMismatch, result.ReasonCode);
    }

    [Fact]
    public void ScenarioA_Coordinate2D_MalformedOrUnsupportedFormat_ReturnsNullAndUnsupportedReason()
    {
        var context = new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.Coordinate2D,
            MaxScore = 40m
        };

        var result = _shortAnswerGrader.Grade("(1,1,1)", "(1,1)", context);

        Assert.Null(result.IsCorrect);
        Assert.Null(result.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.UnsupportedMathFormat, result.ReasonCode);
    }

    // =========================================================================
    // SCENARIO B: Essay Question Pending Teacher Evaluation
    // =========================================================================
    [Fact]
    public async Task ScenarioB_EssayQuestion_PreliminaryNull_CreatesReviewOnlyNeedsTeacherReview_MasteryUnchanged()
    {
        // 1. Arrange Essay question and attempt
        var question = new Question
        {
            QuestionId = 801,
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _topicNodeId,
            Difficulty = 4,
            EstimatedTimeSeconds = 600,
            QuestionText = "Chứng minh hàm số y = x^3 - 3x nghịch biến trên khoảng (-1; 1).",
            CorrectAnswer = "y' = 3x^2 - 3 < 0 trên (-1; 1)",
            Solution = "Đạo hàm y' = 3x^2 - 3. y' < 0 <=> -1 < x < 1.",
            LanguageCode = "vi",
            QuestionType = QuestionType.Essay,
            ReasoningRequired = true,
            Status = QuestionStatus.Active,
            MaxScore = 50m,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        // Deterministic preliminary grading:
        var preliminaryResult = _essayGrader.Grade("Em tính đạo hàm y' = 3x^2 - 3...", question.CorrectAnswer, question.MaxScore, new GradingCriteria());
        Assert.Null(preliminaryResult.IsCorrect);
        Assert.Null(preliminaryResult.Score);
        Assert.Equal(PreliminaryGradingReasonCodes.ManualMode, preliminaryResult.ReasonCode);

        var attempt = new Attempt
        {
            AttemptId = 8001,
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = question.QuestionId,
            FinalAnswer = "Hàm số nghịch biến trên (-1; 1)",
            ReasoningText = "Ta có y' = 3x^2 - 3. y' < 0 với mọi x thuộc (-1; 1)",
            IsCorrect = preliminaryResult.IsCorrect,
            AwardedScore = preliminaryResult.Score,
            PreliminaryGradingReasonCode = preliminaryResult.ReasonCode,
            TimeSpentSeconds = 300,
            Confidence = 85m,
            Skipped = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        var initialKnowledgeTwin = new KnowledgeTwin
        {
            KnowledgeTwinId = 501,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = _topicNodeId,
            MasteryPercentage = 50.00m,
            EvidenceCount = 1,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        _dbContext.Questions.Add(question);
        _dbContext.Attempts.Add(attempt);
        _dbContext.KnowledgeTwins.Add(initialKnowledgeTwin);
        await _dbContext.SaveChangesAsync();

        // 2. AI produces reasoning observation (AI available case)
        var aiResponse = new AnalyzeReasoningResponse
        {
            SchemaVersion = "1.0",
            Language = "vi",
            MethodDetected = "Calculus Derivative Analysis",
            ReasoningQuality = 80,
            ErrorType = ErrorType.None,
            Misconception = null,
            MissingSteps = Array.Empty<string>(),
            RootCauseNodeIds = Array.Empty<string>(),
            Confidence = 85,
            Feedback = "Lập luận chính xác về dấu của đạo hàm.",
            SolutionType = "REFINED",
            AiSolution = "Lời giải chuẩn hoá..."
        };

        var analysis = _analysisBuilder.Build(
            _centerId,
            attempt.AttemptId,
            aiResponse,
            _now,
            attempt.IsCorrect,
            "vi");

        Assert.True(analysis.NeedsTeacherReview);

        // 3. Orchestrator completes analysis
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.AIAnalysis,
            _now,
            CancellationToken.None,
            [_topicNodeId]);

        await _dbContext.SaveChangesAsync();

        // 4. Assert invariants for Scenario B:
        // Attempt remains un-finalized (IsCorrect=null, AwardedScore=null, NeedsTeacherReview)
        Assert.Null(attempt.IsCorrect);
        Assert.Null(attempt.AwardedScore);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, attempt.Status);

        // Evidence Gate decision is ReviewOnly with reasoning weight 0
        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.Evidence.TrustLevel);
        Assert.Equal(0.00m, result.Evidence.ReasoningWeight);
        Assert.True(result.Evidence.RequiresTeacherReview);

        // Knowledge Mastery did NOT change (remains exactly 50.00m)
        var updatedTwin = await _dbContext.KnowledgeTwins
            .SingleAsync(k => k.KnowledgeTwinId == initialKnowledgeTwin.KnowledgeTwinId);
        Assert.Equal(50.00m, updatedTwin.MasteryPercentage);
        Assert.Equal(1u, updatedTwin.EvidenceCount); // not incremented
    }

    // =========================================================================
    // SCENARIO C: Teacher Confirmation Gate & Replay
    // =========================================================================
    [Fact]
    public async Task ScenarioC_TeacherOverride_PersistsProvenance_CreatesEffectiveGrade_AndUpdatesMastery()
    {
        // 1. Arrange: An Essay attempt that went through Scenario B (ReviewOnly, null/null)
        var question = new Question
        {
            QuestionId = 901,
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _topicNodeId,
            Difficulty = 4,
            EstimatedTimeSeconds = 600,
            QuestionText = "Chứng minh hàm số y = x^3 - 3x nghịch biến trên khoảng (-1; 1).",
            CorrectAnswer = "y' < 0",
            Solution = "y' = 3x^2 - 3",
            LanguageCode = "vi",
            QuestionType = QuestionType.Essay,
            ReasoningRequired = true,
            Status = QuestionStatus.Active,
            MaxScore = 50m,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        var attempt = new Attempt
        {
            AttemptId = 9001,
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = question.QuestionId,
            FinalAnswer = "Hàm số nghịch biến trên (-1; 1)",
            ReasoningText = "y' = 3x^2 - 3 < 0",
            IsCorrect = null,
            AwardedScore = null,
            PreliminaryGradingReasonCode = PreliminaryGradingReasonCodes.ManualMode,
            TimeSpentSeconds = 300,
            Confidence = 80m,
            Skipped = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.NeedsTeacherReview,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        var initialKnowledgeTwin = new KnowledgeTwin
        {
            KnowledgeTwinId = 601,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = _topicNodeId,
            MasteryPercentage = 0m,
            EvidenceCount = 0,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        var initialAnalysis = new ReasoningAnalysis
        {
            AnalysisId = 90001,
            CenterId = _centerId,
            AttemptId = attempt.AttemptId,
            SchemaVersion = "1.0",
            ReasoningQuality = 80,
            ErrorType = ErrorType.None,
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            AnalysisConfidence = 85,
            Feedback = "Quan sát tư duy AI: Học sinh hiểu đạo hàm.",
            IsFallback = false,
            NeedsTeacherReview = true,
            OverrideVersion = 0,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        var initialEvidence = new EvidenceAssessment
        {
            EvidenceAssessmentId = 900001,
            CenterId = _centerId,
            AttemptId = attempt.AttemptId,
            AnalysisId = initialAnalysis.AnalysisId,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = EvidenceTrustLevel.ReviewOnly,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = 0.00m,
            RequiresTeacherReview = true,
            PolicyVersion = "evidence-gate-v1",
            ReasonCodes = JsonSerializer.SerializeToDocument(new[] { EvidenceReasonCodes.PreliminaryCorrectnessPending }),
            EvaluatedAt = _now,
            CreatedAt = _now
        };

        _dbContext.Questions.Add(question);
        _dbContext.Attempts.Add(attempt);
        _dbContext.KnowledgeTwins.Add(initialKnowledgeTwin);
        _dbContext.ReasoningAnalyses.Add(initialAnalysis);
        _dbContext.EvidenceAssessments.Add(initialEvidence);
        await _dbContext.SaveChangesAsync();

        // 2. Setup Teacher Context
        var tenantContext = new TenantContext();
        tenantContext.Initialize(_centerId, _teacherId, nameof(UserRole.Teacher), 1);

        var timeProvider = new Mock<TimeProvider>();
        var teacherOverrideTime = _now.AddMinutes(15);
        timeProvider.Setup(tp => tp.GetUtcNow()).Returns(teacherOverrideTime);

        var scopeGuardMock = new Mock<IAttemptTeacherReviewScopeGuard>();
        scopeGuardMock
            .Setup(g => g.CanAccessAttemptAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Attempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var overrideUseCase = new TeacherOverrideUseCase(
            _dbContext,
            tenantContext,
            _gate,
            _evidenceFactory,
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            timeProvider.Object,
            scopeGuard: scopeGuardMock.Object);

        // 3. Teacher confirms grade via TeacherOverride
        var overrideRequest = new TeacherOverrideRequest
        {
            ReasoningQuality = 90,
            ErrorType = ErrorType.None,
            Feedback = "Bài làm xuất sắc, lập luận chặt chẽ.",
            IsCorrect = true,
            AwardedScore = 50m,
            Reason = "Xác nhận điểm tối đa cho phần chứng minh.",
            OverrideVersion = 0
        };

        var overrideResult = await overrideUseCase.ExecuteAsync(
            initialAnalysis.AnalysisId,
            overrideRequest,
            CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, overrideResult.Status);
        Assert.NotNull(overrideResult.Data);

        // 4. Verify Teacher Provenance is persisted
        var updatedAnalysis = await _dbContext.ReasoningAnalyses
            .SingleAsync(a => a.AnalysisId == initialAnalysis.AnalysisId);
        Assert.Equal(_teacherId, updatedAnalysis.OverriddenByUserId);
        Assert.Equal(teacherOverrideTime, updatedAnalysis.OverriddenAt);
        Assert.Equal("Xác nhận điểm tối đa cho phần chứng minh.", updatedAnalysis.OverrideReason);
        Assert.Equal(true, updatedAnalysis.OverrideIsCorrect);
        Assert.Equal(50m, updatedAnalysis.OverrideAwardedScore);
        Assert.Equal(1u, updatedAnalysis.OverrideVersion);
        Assert.False(updatedAnalysis.NeedsTeacherReview);

        // 5. Verify Effective Grade via Feedback UseCase
        var studentGuardMock = new Mock<IStudentOwnershipGuard>();
        studentGuardMock
            .Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var feedbackUseCase = new GetAttemptFeedbackUseCase(_dbContext, tenantContext, studentGuardMock.Object);
        var feedbackResult = await feedbackUseCase.ExecuteAsync(attempt.AttemptId, CancellationToken.None);
        Assert.True(feedbackResult.IsSuccess);
        Assert.NotNull(feedbackResult.Data);
        Assert.Equal(true, feedbackResult.Data.Grading.IsCorrect);
        Assert.Equal(50m, feedbackResult.Data.Grading.AwardedScore);
        Assert.Equal("Teacher", feedbackResult.Data.Grading.Source);
        Assert.Equal(PreliminaryGradingReasonCodes.TeacherOverride, feedbackResult.Data.Grading.ReasonCode);

        // 6. Verify New Evidence is Trusted with HumanConfirmed and weight 1.00
        var latestEvidence = await _dbContext.EvidenceAssessments
            .Where(e => e.AttemptId == attempt.AttemptId)
            .OrderByDescending(e => e.EvaluatedAt)
            .FirstAsync();
        Assert.Equal(EvidenceTrustLevel.Trusted, latestEvidence.TrustLevel);
        Assert.Equal(1.00m, latestEvidence.ReasoningWeight);
        Assert.Equal(EvidenceSourceType.TeacherOverride, latestEvidence.SourceType);
        Assert.Equal(_teacherId, latestEvidence.CreatedBy);

        // 7. Verify Mastery updated from 0 to > 0 via Replay
        var updatedKnowledgeTwin = await _dbContext.KnowledgeTwins
            .SingleAsync(k => k.KnowledgeTwinId == initialKnowledgeTwin.KnowledgeTwinId);
        Assert.True(updatedKnowledgeTwin.MasteryPercentage > 0m,
            $"Expected Mastery to increase above 0m after teacher confirmation, but was {updatedKnowledgeTwin.MasteryPercentage}m");
        Assert.Equal(1u, updatedKnowledgeTwin.EvidenceCount);
    }
}
