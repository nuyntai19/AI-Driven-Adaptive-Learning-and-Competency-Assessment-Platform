using EduTwin.BLL.Assignments;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed class AIStudentPostProcessingTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CoalescesEvents_WithoutPostponingDueTimeIndefinitely()
    {
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(Guid.NewGuid());
        await using var db = Context(Guid.NewGuid().ToString(), tenant); var queue = new AIStudentPostProcessingQueue(db);
        var student = Guid.NewGuid(); var subject = Guid.NewGuid();
        for (ulong n = 1; n <= 50; n++)
        { await queue.EnqueueAsync(db.CurrentTenantId, student, subject, null, n, Now.AddSeconds(n), CancellationToken.None); await db.SaveChangesAsync(); }
        var row = await db.AIStudentPostProcessingJobs.SingleAsync();
        Assert.Equal(50ul, row.Revision); Assert.Equal(Now.AddSeconds(3), row.AvailableAt);
        Assert.Equal(50ul, row.SourceAttemptId); Assert.Equal(0ul, row.ProcessedRevision);
    }

    [Fact]
    public async Task CompletingOldRevisionDoesNotLoseEnqueueDuringProcessing()
    {
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(Guid.NewGuid());
        var name = Guid.NewGuid().ToString(); await using var db = Context(name, tenant);
        var student = Guid.NewGuid(); var subject = Guid.NewGuid(); var assignment = Guid.NewGuid();
        await new AIStudentPostProcessingQueue(db).EnqueueAsync(db.CurrentTenantId, student, subject, assignment, 1, Now, CancellationToken.None);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var recommendations = new Mock<IRecommendationEngine>();
        recommendations.Setup(x => x.GenerateAndPersistAsync(db.CurrentTenantId, student, subject, 1, Now, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await using var other = Context(name, tenant);
                await new AIStudentPostProcessingQueue(other).EnqueueAsync(other.CurrentTenantId, student, subject, assignment, 2, Now.AddSeconds(3), CancellationToken.None);
                await other.SaveChangesAsync(); return RecommendationGenerationResult.NoCandidate();
            });
        var comments = new Mock<IOverallAssignmentCommentWorkflow>();
        comments.Setup(x => x.GenerateAndCacheOverallCommentAsync(db.CurrentTenantId, assignment, student, It.IsAny<CancellationToken>())).ReturnsAsync("summary");
        var processor = new AIStudentPostProcessor(db, recommendations.Object, comments.Object, new Clock(Now.AddSeconds(3)));
        Assert.True(await processor.RunOneAsync(student, subject, assignment, "first-worker", CancellationToken.None));
        var current = await db.AIStudentPostProcessingJobs.SingleAsync();
        Assert.Equal(1ul, current.ProcessedRevision); Assert.Equal(2ul, current.Revision); Assert.Null(current.LeaseOwner);
    }

    [Fact]
    public async Task FailureLeavesDurablePendingWork_AndClearsLeaseForRetry()
    {
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(Guid.NewGuid());
        await using var db = Context(Guid.NewGuid().ToString(), tenant); var student = Guid.NewGuid(); var subject = Guid.NewGuid();
        await new AIStudentPostProcessingQueue(db).EnqueueAsync(db.CurrentTenantId, student, subject, null, 1, Now, CancellationToken.None);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var recommendations = new Mock<IRecommendationEngine>();
        recommendations.Setup(x => x.GenerateAndPersistAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ulong?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic failure"));
        var processor = new AIStudentPostProcessor(db, recommendations.Object, Mock.Of<IOverallAssignmentCommentWorkflow>(), new Clock(Now.AddSeconds(3)));
        Assert.False(await processor.RunOneAsync(student, subject, Guid.Empty, "worker", CancellationToken.None));
        var current = await db.AIStudentPostProcessingJobs.SingleAsync();
        Assert.Equal(0ul, current.ProcessedRevision); Assert.Equal(1, current.FailureCount); Assert.Null(current.LeaseUntil);
        Assert.True(current.AvailableAt > Now.AddSeconds(3));
    }

    private static EduTwinDbContext Context(string name, TenantContext tenant) => new(new DbContextOptionsBuilder<EduTwinDbContext>()
        .UseInMemoryDatabase(name).ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, tenant);
    private sealed class Clock(DateTime value) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(value); }
}
