using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class EffectiveEvidenceAndBoundaryTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    public EffectiveEvidenceAndBoundaryTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: $"EffectiveEvidenceTests_{Guid.NewGuid():N}")
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.Initialize(_centerId, _studentId, nameof(UserRole.Student), 1);
        _dbContext = new EduTwinDbContext(options, _tenantContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void EffectiveCorrectness_UsesOverrideFirst_ThenFallbackToPreliminary()
    {
        // Preliminary null, Override true -> true
        var attempt1 = new Attempt { IsCorrect = null };
        var analysis1 = new ReasoningAnalysis { OverrideIsCorrect = true };
        Assert.True(EffectiveEvidenceResolver.GetEffectiveCorrectness(attempt1, analysis1));

        // Preliminary true, Override false -> false
        var attempt2 = new Attempt { IsCorrect = true };
        var analysis2 = new ReasoningAnalysis { OverrideIsCorrect = false };
        Assert.False(EffectiveEvidenceResolver.GetEffectiveCorrectness(attempt2, analysis2));

        // Preliminary true, Override null -> true
        var attempt3 = new Attempt { IsCorrect = true };
        var analysis3 = new ReasoningAnalysis { OverrideIsCorrect = null };
        Assert.True(EffectiveEvidenceResolver.GetEffectiveCorrectness(attempt3, analysis3));

        // Preliminary null, Analysis null -> null
        var attempt4 = new Attempt { IsCorrect = null };
        Assert.Null(EffectiveEvidenceResolver.GetEffectiveCorrectness(attempt4, null));
    }

    [Fact]
    public void EssayAttempt_PreliminaryNull_TeacherConfirmedTrue_CountsAsOneEffectiveEvidence()
    {
        var attempt = new Attempt
        {
            AttemptId = 101,
            IsCorrect = null,
            StudentId = _studentId
        };
        var analysis = new ReasoningAnalysis
        {
            AnalysisId = 201,
            AttemptId = 101,
            OverrideIsCorrect = true,
            OverrideReasoningQuality = 90m
        };
        var evidence = new EvidenceAssessment
        {
            EvidenceAssessmentId = 301,
            AttemptId = 101,
            Attempt = attempt,
            Analysis = analysis,
            ReasoningWeight = 1.0m,
            TrustLevel = EvidenceTrustLevel.Trusted
        };

        var assessments = new List<EvidenceAssessment> { evidence };
        var heads = EffectiveEvidenceResolver.GetEffectiveHeads(assessments);
        Assert.Single(heads);

        int count = EffectiveEvidenceResolver.CountEffectiveGovernedEvidence(assessments);
        Assert.Equal(1, count);
    }

    [Fact]
    public void SupersededEvidence_DoesNotDoubleCountAttempt()
    {
        var attempt = new Attempt
        {
            AttemptId = 102,
            IsCorrect = false,
            StudentId = _studentId
        };

        // Old AI assessment (superseded)
        var oldEvidence = new EvidenceAssessment
        {
            EvidenceAssessmentId = 301,
            AttemptId = 102,
            Attempt = attempt,
            ReasoningWeight = 0.5m,
            TrustLevel = EvidenceTrustLevel.Reduced
        };

        // New TeacherOverride assessment superseding oldEvidence
        var newEvidence = new EvidenceAssessment
        {
            EvidenceAssessmentId = 302,
            AttemptId = 102,
            Attempt = attempt,
            SupersedesAssessmentId = 301,
            Analysis = new ReasoningAnalysis { OverrideIsCorrect = true, OverrideReasoningQuality = 95m },
            ReasoningWeight = 1.0m,
            TrustLevel = EvidenceTrustLevel.Trusted
        };

        var assessments = new List<EvidenceAssessment> { oldEvidence, newEvidence };
        var heads = EffectiveEvidenceResolver.GetEffectiveHeads(assessments);

        Assert.Single(heads);
        Assert.Equal(302ul, heads[0].EvidenceAssessmentId);

        int count = EffectiveEvidenceResolver.CountEffectiveGovernedEvidence(assessments);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RecommendationEngine_Boundary_2EvidenceUsesLinearFallback_3EvidenceUsesOpportunityGap()
    {
        // Setup topic nodes
        var topic1 = new KnowledgeNode
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = 1,
            NodeCode = "T1",
            NodeName = "Topic 1",
            NodeType = NodeType.Topic,
            OrderIndex = 1,
            ExamImportance = 50m,
            EstimatedLearningMinutes = 60,
            IsActive = true,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var topic2 = new KnowledgeNode
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = 2,
            NodeCode = "T2",
            NodeName = "Topic 2",
            NodeType = NodeType.Topic,
            OrderIndex = 2,
            ExamImportance = 50m,
            EstimatedLearningMinutes = 60,
            IsActive = true,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.KnowledgeNodes.AddRange(topic1, topic2);

        // Student KnowledgeTwin for topic 1 at 30%
        _dbContext.KnowledgeTwins.Add(new KnowledgeTwin
        {
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 1,
            MasteryPercentage = 30m,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        // Add 2 active questions
        _dbContext.Questions.Add(new Question
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 1,
            QuestionId = 1001,
            QuestionText = "Question 1",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "S",
            LanguageCode = "vi",
            Difficulty = 2,
            MaxScore = 10m,
            Status = QuestionStatus.Active,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });

        await _dbContext.SaveChangesAsync();

        var candidateBuilder = new OpportunityCandidateBuilder(_dbContext);
        var linearSelector = new LinearFallbackSelector();
        var questionSelector = new AdaptiveQuestionSelector(_dbContext);
        var engine = new RecommendationEngine(_dbContext, candidateBuilder, linearSelector, questionSelector);

        // Case A: 2 effective evidence records -> LinearFallback
        for (ulong i = 1; i <= 2; i++)
        {
            var att = new Attempt
            {
                CenterId = _centerId,
                AttemptId = i,
                StudentId = _studentId,
                QuestionId = 1001,
                FinalAnswer = "A",
                ReasoningLanguage = "vi",
                Status = AttemptStatus.Completed,
                ClientSubmissionId = Guid.NewGuid(),
                IsCorrect = true,
                CreatedAt = _utcNow.AddMinutes(-(double)i),
                UpdatedAt = _utcNow.AddMinutes(-(double)i)
            };
            att.Question = (await _dbContext.Questions.FindAsync(1001ul))!;
            _dbContext.Attempts.Add(att);

            var ana = new ReasoningAnalysis
            {
                CenterId = _centerId,
                AnalysisId = 200 + i,
                AttemptId = i,
                SchemaVersion = "v1",
                MissingSteps = JsonDocument.Parse("[]"),
                RootCauseNodeIds = JsonDocument.Parse("[]"),
                Feedback = "Good reasoning",
                ReasoningQuality = 80m,
                CreatedAt = _utcNow.AddMinutes(-(double)i),
                UpdatedAt = _utcNow.AddMinutes(-(double)i)
            };
            _dbContext.ReasoningAnalyses.Add(ana);

            _dbContext.EvidenceAssessments.Add(new EvidenceAssessment
            {
                CenterId = _centerId,
                EvidenceAssessmentId = 300 + i,
                AttemptId = i,
                Attempt = att,
                AnalysisId = 200 + i,
                Analysis = ana,
                ReasoningWeight = 1.0m,
                TrustLevel = EvidenceTrustLevel.Trusted,
                PolicyVersion = "v1",
                ReasonCodes = JsonDocument.Parse("[]"),
                EvaluatedAt = _utcNow.AddMinutes(-(double)i),
                CreatedAt = _utcNow.AddMinutes(-(double)i)
            });
        }
        await _dbContext.SaveChangesAsync();

        var recFallback = await engine.GenerateAndPersistAsync(
            _centerId,
            _studentId,
            _subjectId,
            sourceAttemptId: 2,
            _utcNow,
            CancellationToken.None);

        Assert.NotNull(recFallback);
        Assert.Equal(RecommendationType.LinearFallback, recFallback.RecommendationType);

        var pathFallback = await _dbContext.LearningPaths
            .FirstOrDefaultAsync(lp => lp.CenterId == _centerId && lp.StudentId == _studentId && lp.Status == LearningPathStatus.Active);
        Assert.NotNull(pathFallback);
        Assert.Equal(LearningPathStrategy.LinearFallback, pathFallback.Strategy);

        // Case B: Add 3rd effective evidence record -> OpportunityGap
        var att3 = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 3,
            StudentId = _studentId,
            QuestionId = 1001,
            FinalAnswer = "A",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            ClientSubmissionId = Guid.NewGuid(),
            IsCorrect = true,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        att3.Question = (await _dbContext.Questions.FindAsync(1001ul))!;
        _dbContext.Attempts.Add(att3);

        var ana3 = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 203,
            AttemptId = 3,
            SchemaVersion = "v1",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            Feedback = "Good reasoning",
            ReasoningQuality = 85m,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.ReasoningAnalyses.Add(ana3);

        _dbContext.EvidenceAssessments.Add(new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 303,
            AttemptId = 3,
            Attempt = att3,
            AnalysisId = 203,
            Analysis = ana3,
            ReasoningWeight = 1.0m,
            TrustLevel = EvidenceTrustLevel.Trusted,
            PolicyVersion = "v1",
            ReasonCodes = JsonDocument.Parse("[]"),
            EvaluatedAt = _utcNow,
            CreatedAt = _utcNow
        });
        await _dbContext.SaveChangesAsync();

        var recOpportunity = await engine.GenerateAndPersistAsync(
            _centerId,
            _studentId,
            _subjectId,
            sourceAttemptId: 3,
            _utcNow,
            CancellationToken.None);

        Assert.NotNull(recOpportunity);
        Assert.Equal(RecommendationType.TopicAndQuestion, recOpportunity.RecommendationType);
        Assert.NotNull(recOpportunity.OpportunityScore);

        var pathOpportunity = await _dbContext.LearningPaths
            .FirstOrDefaultAsync(lp => lp.CenterId == _centerId && lp.StudentId == _studentId && lp.Status == LearningPathStatus.Active);
        Assert.NotNull(pathOpportunity);
        Assert.Equal(LearningPathStrategy.OpportunityGap, pathOpportunity.Strategy);
    }
}
