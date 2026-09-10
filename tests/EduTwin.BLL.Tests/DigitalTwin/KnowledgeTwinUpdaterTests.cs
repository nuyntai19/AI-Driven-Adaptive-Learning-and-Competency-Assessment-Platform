using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.DigitalTwin;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.DigitalTwin;

public sealed class KnowledgeTwinUpdaterTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();

    public KnowledgeTwinUpdaterTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task UpdateAsync_TrustedEvidence_IncreasesMasteryAndIncrementsEvidenceCount()
    {
        var updater = new KnowledgeTwinUpdater(_dbContext);
        var now = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1001,
            StudentId = _studentId,
            QuestionId = 501,
            TimeSpentSeconds = 60,
            Confidence = 90m,
            IsCorrect = true
        };

        var question = new Question
        {
            CenterId = _centerId,
            QuestionId = 501,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 201,
            Difficulty = 3,
            EstimatedTimeSeconds = 60
        };

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 2001,
            AttemptId = 1001,
            ReasoningQuality = 85m,
            AnalysisConfidence = 90m
        };

        var evidence = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 3001,
            AttemptId = 1001,
            ReasoningWeight = 1.00m,
            TrustLevel = EvidenceTrustLevel.Trusted
        };

        var result = await updater.UpdateAsync(
            attempt,
            question,
            analysis,
            evidence,
            confidenceCalibration: 80m,
            now,
            CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.NotNull(result.Twin);
        Assert.True(result.Twin.MasteryPercentage > 0m);
        Assert.Equal(1u, result.Twin.EvidenceCount);
        Assert.Equal(85m, result.Twin.LastReasoningQuality);
        Assert.Equal(1001u, result.Twin.LastAttemptId);
        Assert.Equal(now, result.Twin.LastEvidenceAt);
    }

    [Fact]
    public async Task UpdateAsync_ReviewOnlyEvidence_PreservesMasteryAndDoesNotIncrementCount()
    {
        var updater = new KnowledgeTwinUpdater(_dbContext);
        var now = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

        // Pre-existing knowledge twin with 50% mastery
        var existingTwin = new KnowledgeTwin
        {
            CenterId = _centerId,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = 201,
            MasteryPercentage = 50.00m,
            EvidenceCount = 3,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.KnowledgeTwins.Add(existingTwin);
        await _dbContext.SaveChangesAsync();

        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 1002,
            StudentId = _studentId,
            QuestionId = 501,
            TimeSpentSeconds = 60,
            Confidence = 40m,
            IsCorrect = false
        };

        var question = new Question
        {
            CenterId = _centerId,
            QuestionId = 501,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 201,
            Difficulty = 3,
            EstimatedTimeSeconds = 60
        };

        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 2002,
            AttemptId = 1002,
            ReasoningQuality = 30m,
            AnalysisConfidence = 35m,
            IsFallback = true
        };

        var evidence = new EvidenceAssessment
        {
            CenterId = _centerId,
            EvidenceAssessmentId = 3002,
            AttemptId = 1002,
            ReasoningWeight = 0.00m,
            TrustLevel = EvidenceTrustLevel.ReviewOnly
        };

        var result = await updater.UpdateAsync(
            attempt,
            question,
            analysis,
            evidence,
            confidenceCalibration: 50m,
            now,
            CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.Equal(50.00m, result.Twin.MasteryPercentage);
        Assert.Equal(3u, result.Twin.EvidenceCount); // not incremented
        Assert.Equal(0.00m, result.Calculation.Delta);
    }
}
