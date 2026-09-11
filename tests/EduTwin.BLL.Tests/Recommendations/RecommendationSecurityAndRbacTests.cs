using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.Recommendations;
using EduTwin.BLL.Recommendations.UseCases;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Organization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class RecommendationSecurityAndRbacTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _student1Id = Guid.NewGuid();
    private readonly Guid _student2Id = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    public RecommendationSecurityAndRbacTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: $"RecSecurityTests_{Guid.NewGuid():N}")
            .Options;

        _tenantContext = new TenantContext();
        _dbContext = new EduTwinDbContext(options, _tenantContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetActiveRecommendation_UnresolvedTenant_ReturnsForbidden()
    {
        var engine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        var useCase = new GetActiveRecommendationUseCase(_dbContext, _tenantContext, engine);
        var result = await useCase.ExecuteAsync(null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.Forbidden);
    }

    [Fact]
    public async Task AcceptRecommendation_CrossStudentOwnership_ReturnsNotFound()
    {
        // Seed student 1 and student 2 in the center
        _dbContext.Students.AddRange(
            new Student { CenterId = _centerId, StudentId = _student1Id, FullName = "Student 1", CreatedAt = _utcNow, UpdatedAt = _utcNow },
            new Student { CenterId = _centerId, StudentId = _student2Id, FullName = "Student 2", CreatedAt = _utcNow, UpdatedAt = _utcNow }
        );

        // Recommendation belongs to Student 1
        var recStudent1 = new Recommendation
        {
            RecommendationId = 1001,
            CenterId = _centerId,
            StudentId = _student1Id,
            SubjectId = _subjectId,
            TopicNodeId = 1,
            RecommendationType = RecommendationType.TopicAndQuestion,
            CalculationVersion = "opportunity-v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Rec for Student 1",
            Status = RecommendationStatus.Active,
            GeneratedAt = _utcNow,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Recommendations.Add(recStudent1);
        await _dbContext.SaveChangesAsync();

        // Authenticate as Student 2
        _tenantContext.Initialize(_centerId, _student2Id, "Student", 1);

        var engine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        var acceptUseCase = new AcceptRecommendationUseCase(_dbContext, _tenantContext, engine, TimeProvider.System);

        // Student 2 tries to accept Student 1's recommendation
        var result = await acceptUseCase.ExecuteAsync(recStudent1.RecommendationId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.NotFound); // Cannot see or accept other student's recommendation
    }

    [Fact]
    public async Task GetNextQuestion_StudentNotInCenter_ReturnsNotFound()
    {
        // Tenant context has non-existent student
        _tenantContext.Initialize(_centerId, Guid.NewGuid(), "Student", 1);

        var engine = new RecommendationEngine(
            _dbContext,
            new OpportunityCandidateBuilder(_dbContext),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(_dbContext));

        var useCase = new GetNextQuestionUseCase(_dbContext, _tenantContext, engine, TimeProvider.System);
        var result = await useCase.ExecuteAsync(_subjectId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.NotFound);
    }
}
