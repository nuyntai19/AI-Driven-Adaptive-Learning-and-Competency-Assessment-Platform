using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.BLL.Tests.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class CyclicPrerequisiteNonRollbackTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    public CyclicPrerequisiteNonRollbackTests()
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

    private void SeedCyclicKnowledgeGraph()
    {
        _dbContext.Students.Add(new Student
        {
            CenterId = _centerId,
            StudentId = _studentId,
            FullName = "Test Student",
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        // Topic 1 and Topic 2
        _dbContext.KnowledgeNodes.AddRange(
            new KnowledgeNode
            {
                CenterId = _centerId,
                NodeId = 101,
                SubjectId = _subjectId,
                NodeCode = "TOPIC_1",
                NodeType = NodeType.Topic,
                NodeName = "Topic 1",
                OrderIndex = 1,
                ExamImportance = 50m,
                EstimatedLearningMinutes = 60,
                IsActive = true,
                CreatedAt = _utcNow,
                UpdatedAt = _utcNow
            },
            new KnowledgeNode
            {
                CenterId = _centerId,
                NodeId = 102,
                SubjectId = _subjectId,
                NodeCode = "TOPIC_2",
                NodeType = NodeType.Topic,
                NodeName = "Topic 2",
                OrderIndex = 2,
                ExamImportance = 50m,
                EstimatedLearningMinutes = 60,
                IsActive = true,
                CreatedAt = _utcNow,
                UpdatedAt = _utcNow
            }
        );

        // Cyclic prerequisite: 101 -> 102 and 102 -> 101
        _dbContext.KnowledgeEdges.AddRange(
            new KnowledgeEdge
            {
                CenterId = _centerId,
                EdgeId = 1,
                SubjectId = _subjectId,
                SourceNodeId = 101,
                TargetNodeId = 102,
                RelationType = RelationType.PrerequisiteOf,
                CreatedAt = _utcNow,
                UpdatedAt = _utcNow
            },
            new KnowledgeEdge
            {
                CenterId = _centerId,
                EdgeId = 2,
                SubjectId = _subjectId,
                SourceNodeId = 102,
                TargetNodeId = 101,
                RelationType = RelationType.PrerequisiteOf,
                CreatedAt = _utcNow,
                UpdatedAt = _utcNow
            }
        );

        _dbContext.Questions.Add(new Question
        {
            CenterId = _centerId,
            QuestionId = 501,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 101,
            QuestionText = "Question on Topic 1",
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

        _dbContext.QuestionKnowledgeNodes.Add(new QuestionKnowledgeNode
        {
            CenterId = _centerId,
            QuestionId = 501,
            NodeId = 101,
            MappingRole = MappingRole.Primary,
            CreatedAt = _utcNow
        });
    }

    [Fact]
    public async Task AIAnalysisJobProcessor_WhenPrerequisiteCycle_DoesNotRollbackEvidenceOrTwin()
    {
        SeedCyclicKnowledgeGraph();

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1001,
            StudentId = _studentId,
            QuestionId = 501,
            FinalAnswer = "A",
            ReasoningText = "Here is my reasoning for why A is correct",
            TimeSpentSeconds = 60,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            ClientSubmissionId = Guid.NewGuid(),
            IsCorrect = true,
            AwardedScore = 10m,
            Skipped = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Attempts.Add(attempt);

        var job = new AIAnalysisJob
        {
            CenterId = _centerId,
            AnalysisJobId = 2001,
            AttemptId = attempt.AttemptId,
            Status = AIJobStatus.Processing,
            AvailableAt = _utcNow,
            LeaseOwner = "worker-1",
            LeaseUntil = _utcNow.AddMinutes(5),
            CorrelationId = Guid.NewGuid().ToString("D"),
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.AIAnalysisJobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var recEngine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        var aiResponse = new AnalyzeReasoningResponse
        {
            SchemaVersion = "v1",
            Language = "vi",
            ReasoningQuality = 80,
            ErrorType = ErrorType.None,
            MissingSteps = Array.Empty<string>(),
            RootCauseNodeIds = Array.Empty<string>(),
            Confidence = 95,
            Feedback = "Accurate analysis"
        };

        var mockAiService = new Mock<IAIService>();
        mockAiService.Setup(s => s.AnalyzeReasoningAsync(It.IsAny<AnalyzeReasoningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiResponse);

        // System tenant context for background processor
        var processorTenant = new TenantContext();
        using var scope = processorTenant.BeginScope(_centerId);

        var processor = new AIAnalysisJobProcessor(
            _dbContext,
            processorTenant,
            mockAiService.Object,
            new AIAnalysisRequestFactory(),
            new AIReasoningAnalysisBuilder(),
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new FixedTimeProvider(_utcNow),
            recommendationEngine: recEngine);

        var outcome = await processor.ExecuteAsync(job.AnalysisJobId, "worker-1", CancellationToken.None);

        Assert.Equal(AIAnalysisJobProcessingOutcome.Completed, outcome.Outcome);

        // Verify Job completed
        var reloadedJob = await _dbContext.AIAnalysisJobs.FindAsync(job.AnalysisJobId);
        Assert.NotNull(reloadedJob);
        Assert.Equal(AIJobStatus.Completed, reloadedJob.Status);

        // Verify Evidence Assessment committed
        var evidence = await _dbContext.EvidenceAssessments
            .FirstOrDefaultAsync(e => e.CenterId == _centerId && e.AttemptId == attempt.AttemptId);
        Assert.NotNull(evidence);

        // Verify KnowledgeTwin committed
        var twin = await _dbContext.KnowledgeTwins
            .FirstOrDefaultAsync(t => t.CenterId == _centerId && t.StudentId == _studentId && t.TopicNodeId == 101);
        Assert.NotNull(twin);
        Assert.True(twin.MasteryPercentage > 0m);
    }

    [Fact]
    public async Task TeacherOverride_WhenPrerequisiteCycle_DoesNotRollbackEvidenceOrTwin()
    {
        SeedCyclicKnowledgeGraph();

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3001,
            StudentId = _studentId,
            QuestionId = 501,
            FinalAnswer = "A",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            ClientSubmissionId = Guid.NewGuid(),
            IsCorrect = false,
            AwardedScore = 0m,
            Skipped = false,
            CreatedAt = _utcNow.AddHours(-1),
            UpdatedAt = _utcNow.AddHours(-1)
        };
        _dbContext.Attempts.Add(attempt);

        var initialAnalysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 4001,
            AttemptId = attempt.AttemptId,
            SchemaVersion = "v1",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            Feedback = "Initial analysis",
            ReasoningQuality = 40m,
            CreatedAt = _utcNow.AddHours(-1),
            UpdatedAt = _utcNow.AddHours(-1)
        };
        _dbContext.ReasoningAnalyses.Add(initialAnalysis);

        var initialEvidence = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 5001,
            AttemptId = attempt.AttemptId,
            Attempt = attempt,
            AnalysisId = initialAnalysis.AnalysisId,
            Analysis = initialAnalysis,
            ReasoningWeight = 1.0m,
            TrustLevel = EvidenceTrustLevel.Trusted,
            PolicyVersion = "v1",
            ReasonCodes = JsonDocument.Parse("[]"),
            EvaluatedAt = _utcNow.AddHours(-1),
            CreatedAt = _utcNow.AddHours(-1)
        };
        _dbContext.EvidenceAssessments.Add(initialEvidence);

        var classId = Guid.NewGuid();
        _dbContext.Classes.Add(new Class
        {
            CenterId = _centerId,
            ClassId = classId,
            TeacherId = _teacherId,
            SubjectId = _subjectId,
            ClassName = "Class 10A",
            AcademicYear = "2025-2026",
            Status = ClassStatus.Active,
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

        var initialTwin = new KnowledgeTwin
        {
            KnowledgeTwinId = 1ul,
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 101,
            MasteryPercentage = 20m,
            EvidenceCount = 1,
            CreatedAt = _utcNow.AddHours(-1),
            UpdatedAt = _utcNow.AddHours(-1)
        };
        _dbContext.KnowledgeTwins.Add(initialTwin);
        await _dbContext.SaveChangesAsync();

        var recEngine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        var useCase = new TeacherOverrideUseCase(
            _dbContext,
            _tenantContext,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(_dbContext),
            new StudentTwinUpdater(_dbContext),
            new TwinUpdateHistoryWriter(_dbContext),
            new FixedTimeProvider(_utcNow),
            recommendationEngine: recEngine);

        var request = new TeacherOverrideRequest
        {
            IsCorrect = true,
            ReasoningQuality = 85m,
            AwardedScore = 10m,
            Reason = "Teacher marked correct upon review"
        };

        var result = await useCase.ExecuteAsync(initialAnalysis.AnalysisId, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.Replay.RecommendationRecalculated); // Prerequisite cycle prevented recommendation, but override succeeded!

        // Verify that TeacherOverride committed to database
        var latestEvidence = await _dbContext.EvidenceAssessments
            .Where(e => e.CenterId == _centerId && e.AttemptId == attempt.AttemptId)
            .OrderByDescending(e => e.EvaluatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(latestEvidence);
        Assert.Equal(EvidenceSourceType.TeacherOverride, latestEvidence.SourceType);
        Assert.Equal(initialEvidence.EvidenceAssessmentId, latestEvidence.SupersedesAssessmentId);

        // Verify KnowledgeTwin replayed mastery committed
        var reloadedTwin = await _dbContext.KnowledgeTwins
            .FirstOrDefaultAsync(t => t.CenterId == _centerId && t.StudentId == _studentId && t.TopicNodeId == 101);
        Assert.NotNull(reloadedTwin);
        Assert.True(reloadedTwin.MasteryPercentage > 0m);
        Assert.Equal(result.Data.Replay.NewMastery, reloadedTwin.MasteryPercentage);
    }
}
