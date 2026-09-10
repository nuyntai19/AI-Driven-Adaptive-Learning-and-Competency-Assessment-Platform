using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.DigitalTwin.Orchestration;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Evidence;

/// <summary>
/// Proves the 8 mandatory integration and orchestration scenarios required by architectural review.
/// </summary>
public sealed class EvidenceGateIntegrationScenariosTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly ulong _topicNodeId = 101;
    private readonly DateTime _now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly EvidenceGate _gate = new();
    private readonly EvidenceConsistencyChecker _consistencyChecker = new();
    private readonly EvidenceAssessmentFactory _factory = new();

    public EvidenceGateIntegrationScenariosTests()
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

    private (Attempt Attempt, Question Question, ReasoningAnalysis Analysis) CreateSetup(
        ulong attemptId = 101,
        bool? isCorrect = true,
        int quality = 85,
        ErrorType errorType = ErrorType.None,
        int confidence = 85,
        string[]? rootCauses = null)
    {
        var attempt = new Attempt
        {
            AttemptId = attemptId,
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = 501,
            FinalAnswer = "42",
            ReasoningText = "Valid reasoning steps...",
            IsCorrect = isCorrect,
            TimeSpentSeconds = 60,
            Confidence = 80m,
            Skipped = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        var question = new Question
        {
            QuestionId = 501,
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _topicNodeId,
            Difficulty = 3,
            EstimatedTimeSeconds = 60,
            QuestionText = "Question text",
            CorrectAnswer = "42",
            Solution = "Solution text",
            LanguageCode = "vi",
            ReasoningRequired = true,
            Status = QuestionStatus.Active,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        var rootCausesJson = JsonSerializer.SerializeToDocument(rootCauses ?? Array.Empty<string>());
        var missingStepsJson = JsonSerializer.SerializeToDocument(Array.Empty<string>());

        var analysis = new ReasoningAnalysis
        {
            AnalysisId = attemptId + 1000,
            CenterId = _centerId,
            AttemptId = attemptId,
            SchemaVersion = "1.0",
            ReasoningQuality = quality,
            ErrorType = errorType,
            MissingSteps = missingStepsJson,
            RootCauseNodeIds = rootCausesJson,
            AnalysisConfidence = confidence,
            Feedback = "Detailed feedback",
            IsFallback = false,
            NeedsTeacherReview = false,
            CreatedAt = _now,
            UpdatedAt = _now
        };

        return (attempt, question, analysis);
    }

    private TwinCompletionOrchestrator CreateOrchestrator()
    {
        return new TwinCompletionOrchestrator(
            _dbContext,
            _gate,
            _factory,
            new BehaviorTwinUpdater(_dbContext),
            new KnowledgeTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            _consistencyChecker);
    }

    // Case 1: Student is incorrect, AI has high confidence (95) but claims ErrorType.None -> NEVER Trusted, must be ReviewOnly with weight 0
    [Fact]
    public async Task Scenario1_IncorrectStudent_AIHighConfidence_ClaimsNoError_BecomesReviewOnlyWeightZero()
    {
        var (attempt, question, analysis) = CreateSetup(
            isCorrect: false,
            quality: 90,
            errorType: ErrorType.None,
            confidence: 95);

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.AIAnalysis,
            _now,
            CancellationToken.None);

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.Evidence.TrustLevel);
        Assert.Equal(0.00m, result.Evidence.ReasoningWeight);
        Assert.True(result.Evidence.RequiresTeacherReview);
        Assert.Contains(EvidenceReasonCodes.ContradictionDetected, result.Evidence.ReasonCodes.RootElement.ToString());
        Assert.Equal(AttemptStatus.NeedsTeacherReview, attempt.Status);
    }

    // Case 2: AI returns root cause IDs that mismatch context/allowed nodes -> ReviewOnly, weight 0
    [Fact]
    public async Task Scenario2_AIRootCauseMismatchAllowedNodes_BecomesReviewOnlyWeightZero()
    {
        var (attempt, question, analysis) = CreateSetup(
            isCorrect: false,
            quality: 50,
            errorType: ErrorType.Knowledge,
            confidence: 85,
            rootCauses: ["999"]); // Foreign node ID

        var allowedNodes = new ulong[] { _topicNodeId }; // only 101 allowed
        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.AIAnalysis,
            _now,
            CancellationToken.None,
            allowedNodes);

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.Evidence.TrustLevel);
        Assert.Equal(0.00m, result.Evidence.ReasoningWeight);
        Assert.True(result.Evidence.RequiresTeacherReview);
        Assert.Contains(EvidenceReasonCodes.SemanticValidationFailed, result.Evidence.ReasonCodes.RootElement.ToString());
    }

    // Case 3: Essay question with IsCorrect = null, AI confidence 99% -> ReviewOnly, weight 0
    [Fact]
    public async Task Scenario3_EssayNullCorrectness_HighConfidence_BecomesReviewOnlyWeightZero()
    {
        var (attempt, question, analysis) = CreateSetup(
            isCorrect: null,
            quality: 92,
            errorType: ErrorType.None,
            confidence: 99);
        question.QuestionType = QuestionType.Essay;

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.AIAnalysis,
            _now,
            CancellationToken.None);

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.Evidence.TrustLevel);
        Assert.Equal(0.00m, result.Evidence.ReasoningWeight);
        Assert.True(result.Evidence.RequiresTeacherReview);
        Assert.Contains(EvidenceReasonCodes.PreliminaryCorrectnessPending, result.Evidence.ReasonCodes.RootElement.ToString());
        Assert.Equal(AttemptStatus.NeedsTeacherReview, attempt.Status);
    }

    // Case 4: Gemini timeout -> Fallback -> Knowledge Mastery remains completely unchanged
    [Fact]
    public async Task Scenario4_GeminiTimeoutFallback_MasteryRemainsUnchanged()
    {
        // Seed initial KnowledgeTwin
        var initialTwin = new KnowledgeTwin
        {
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = _topicNodeId,
            MasteryPercentage = 65.00m,
            EvidenceCount = 5,
            LastReasoningQuality = 70.00m,
            LastAttemptId = 99,
            LastEvidenceAt = _now.AddDays(-1),
            CreatedAt = _now.AddDays(-5),
            UpdatedAt = _now.AddDays(-1)
        };
        _dbContext.KnowledgeTwins.Add(initialTwin);
        await _dbContext.SaveChangesAsync();

        var (attempt, question, analysis) = CreateSetup(isCorrect: true);
        analysis.IsFallback = true;
        analysis.ReasoningQuality = null;
        analysis.AnalysisConfidence = null;

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.RuleFallback,
            _now,
            CancellationToken.None);

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.Evidence.TrustLevel);
        Assert.Equal(0.00m, result.Evidence.ReasoningWeight);
        // Mastery must not change
        Assert.Equal(65.00m, result.KnowledgeTwin.MasteryPercentage);
        Assert.Equal(5u, result.KnowledgeTwin.EvidenceCount);
    }

    // Case 5: AI valid + consistent + confidence 80% -> Trusted, weight 1.0
    [Fact]
    public async Task Scenario5_ConsistentAI_Confidence80_BecomesTrustedWeightOne()
    {
        var (attempt, question, analysis) = CreateSetup(
            isCorrect: true,
            quality: 85,
            errorType: ErrorType.None,
            confidence: 80);

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.AIAnalysis,
            _now,
            CancellationToken.None,
            [_topicNodeId]);

        Assert.Equal(EvidenceTrustLevel.Trusted, result.Evidence.TrustLevel);
        Assert.Equal(1.00m, result.Evidence.ReasoningWeight);
        Assert.False(result.Evidence.RequiresTeacherReview);
        Assert.Equal(AttemptStatus.Completed, attempt.Status);
    }

    // Case 6: Confidence boundaries 49/50 and 79/80
    [Theory]
    [InlineData(49, EvidenceTrustLevel.ReviewOnly, "0.00", true)]
    [InlineData(50, EvidenceTrustLevel.Reduced, "0.50", false)]
    [InlineData(79, EvidenceTrustLevel.Reduced, "0.50", false)]
    [InlineData(80, EvidenceTrustLevel.Trusted, "1.00", false)]
    public void Scenario6_ConfidenceBoundaries_MapToCorrectTrustAndWeight(
        int confidence,
        EvidenceTrustLevel expectedTrust,
        string expectedWeight,
        bool expectedReview)
    {
        var (attempt, question, analysis) = CreateSetup(
            isCorrect: true,
            quality: 80,
            errorType: ErrorType.None,
            confidence: confidence);

        var consistency = _consistencyChecker.Evaluate(attempt, question, analysis, [_topicNodeId]);
        var gateDecision = _gate.Evaluate(new EvidenceGateInput(
            EvidenceSourceType.AI,
            consistency.StructuralValidationPassed,
            consistency.SemanticValidationPassed,
            consistency.HasContradiction,
            consistency.HasAnomaly,
            consistency.HasRequiredEvidence,
            attempt.IsCorrect,
            analysis.AnalysisConfidence,
            analysis.OverrideVersion));

        Assert.Equal(expectedTrust, gateDecision.TrustLevel);
        Assert.Equal(decimal.Parse(expectedWeight), gateDecision.ReasoningWeight);
        Assert.Equal(expectedReview, gateDecision.RequiresTeacherReview);
    }

    // Case 7: Teacher override after ReviewOnly creates new superseding evidence with Trusted, old evidence preserved
    [Fact]
    public async Task Scenario7_TeacherOverrideAfterReviewOnly_CreatesNewEvidenceAndPreservesOld()
    {
        // 1. Initial attempt lands in ReviewOnly
        var (attempt, question, analysis) = CreateSetup(
            isCorrect: false,
            quality: 90,
            errorType: ErrorType.None, // Contradiction -> ReviewOnly
            confidence: 95);

        _dbContext.Questions.Add(question);
        _dbContext.Attempts.Add(attempt);
        await _dbContext.SaveChangesAsync();

        var orchestrator = CreateOrchestrator();
        var initialResult = await orchestrator.CompleteAsync(
            attempt,
            question,
            analysis,
            TwinEventSource.AIAnalysis,
            _now,
            CancellationToken.None);

        await _dbContext.SaveChangesAsync();

        var initialEvidenceId = initialResult.Evidence.EvidenceAssessmentId;
        Assert.Equal(EvidenceTrustLevel.ReviewOnly, initialResult.Evidence.TrustLevel);

        // 2. Teacher executes override
        var teacherId = Guid.NewGuid();
        var overrideGateDecision = _gate.Evaluate(new EvidenceGateInput(
            SourceType: EvidenceSourceType.TeacherOverride,
            StructuralValidationPassed: true,
            SemanticValidationPassed: true,
            HasContradiction: false,
            HasAnomaly: false,
            HasRequiredEvidence: true,
            EffectiveIsCorrect: true,
            AnalysisConfidence: null,
            AnalysisOverrideVersion: 1));

        var newEvidence = _factory.Create(
            attempt,
            analysis,
            initialResult.Evidence,
            overrideGateDecision,
            _now.AddMinutes(10),
            teacherId);

        _dbContext.EvidenceAssessments.Add(newEvidence);
        await _dbContext.SaveChangesAsync();

        // 3. Verify both records exist in DB and lineage is preserved
        var allEvidence = await _dbContext.EvidenceAssessments
            .Where(e => e.AttemptId == attempt.AttemptId)
            .OrderBy(e => e.EvaluatedAt)
            .ToListAsync();

        Assert.Equal(2, allEvidence.Count);
        Assert.Equal(initialEvidenceId, allEvidence[0].EvidenceAssessmentId);
        Assert.Equal(EvidenceTrustLevel.ReviewOnly, allEvidence[0].TrustLevel);

        Assert.Equal(EvidenceTrustLevel.Trusted, allEvidence[1].TrustLevel);
        Assert.Equal(1.00m, allEvidence[1].ReasoningWeight);
        Assert.Equal(initialEvidenceId, allEvidence[1].SupersedesAssessmentId);
        Assert.Equal(EvidenceSourceType.TeacherOverride, allEvidence[1].SourceType);
    }

    // Case 8: Replay determinism - multiple attempts replayed sequentially from baseline 0.00m without double-applying
    [Fact]
    public void Scenario8_SequentialReplay_DoesNotDoubleApplyEvidence()
    {
        var baselineMastery = 0.00m;

        // Attempt 1: Trusted, weight 1.0, quality 80, correct
        var input1 = new MasteryCalculationInput(
            CurrentMastery: baselineMastery,
            ReasoningQuality: 80m,
            ReasoningWeight: 1.00m,
            TimeQuality: 1.0m,
            ConfidenceCalibration: 1.0m,
            IsCorrect: true,
            Difficulty: 3);
        var result1 = MasteryCalculator.Calculate(input1);

        Assert.True(result1.NewMastery > baselineMastery);

        // Attempt 2: ReviewOnly, weight 0.0, quality 90 (contradictory)
        var input2 = new MasteryCalculationInput(
            CurrentMastery: result1.NewMastery,
            ReasoningQuality: 90m,
            ReasoningWeight: 0.00m, // weight 0 means NO mastery change!
            TimeQuality: 1.0m,
            ConfidenceCalibration: 1.0m,
            IsCorrect: false,
            Difficulty: 3);
        var result2 = MasteryCalculator.Calculate(input2);

        Assert.Equal(result1.NewMastery, result2.NewMastery); // Mastery is strictly preserved!

        // Attempt 3: Teacher override for Attempt 2: weight 1.0, quality 75, correct
        var input3 = new MasteryCalculationInput(
            CurrentMastery: result2.NewMastery,
            ReasoningQuality: 75m,
            ReasoningWeight: 1.00m,
            TimeQuality: 1.0m,
            ConfidenceCalibration: 1.0m,
            IsCorrect: true,
            Difficulty: 3);
        var result3 = MasteryCalculator.Calculate(input3);

        Assert.True(result3.NewMastery > result2.NewMastery);
    }
}
