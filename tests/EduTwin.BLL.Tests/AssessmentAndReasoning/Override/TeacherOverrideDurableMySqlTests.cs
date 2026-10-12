using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Override;

public sealed partial class TeacherOverrideMySqlTests
{
    [MySqlIntegrationFact]
    public async Task ExecuteAsync_DurableOverrideQueuesLearningRefreshInTheAuthoritativeTransaction()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var center = Guid.NewGuid(); var teacher = Guid.NewGuid(); var student = Guid.NewGuid(); var subject = Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString, center, teacher, student, subject);
        var tenant = new TenantContext(); tenant.Initialize(center, teacher, nameof(UserRole.Teacher), 1);
        await using var db = CreateContext(database.ConnectionString, tenant);
        var useCase = new TeacherOverrideUseCase(db, tenant, new EvidenceGate(), new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(db), new StudentTwinUpdater(db), new TwinUpdateHistoryWriter(db), new FixedTimeProvider(UtcNow),
            postProcessing: new AIStudentPostProcessingQueue(db));
        var result = await useCase.ExecuteAsync(2001, new TeacherOverrideRequest
        {
            ReasoningQuality = 88, ErrorType = ErrorType.Presentation, Feedback = "Teacher confirmed another valid method.",
            IsCorrect = true, Reason = "Synthetic teacher evaluation", OverrideVersion = 0
        }, CancellationToken.None);
        Assert.Equal(TeacherOverrideStatus.Success, result.Status);
        await using var verify = CreateContext(database.ConnectionString, tenant);
        var queued = await verify.AIStudentPostProcessingJobs.SingleAsync();
        Assert.Equal(center, queued.CenterId); Assert.Equal(student, queued.StudentId); Assert.Equal(subject, queued.SubjectId);
        Assert.Equal(1001ul, queued.SourceAttemptId); Assert.Equal(1ul, queued.Revision); Assert.Equal(0ul, queued.ProcessedRevision);
        Assert.Equal(1u, (await verify.ReasoningAnalyses.SingleAsync(x => x.AnalysisId == 2001)).OverrideVersion);
        Assert.True((await verify.EvidenceAssessments.SingleAsync(x => x.AnalysisOverrideVersion == 1)).SourceType == EvidenceSourceType.TeacherOverride);
    }
}
