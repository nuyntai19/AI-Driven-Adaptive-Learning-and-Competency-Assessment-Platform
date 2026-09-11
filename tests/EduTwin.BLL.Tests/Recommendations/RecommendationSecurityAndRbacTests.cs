using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using EduTwin.API.Controllers;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.BLL.Recommendations.UseCases;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Organization;
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

    [Theory]
    [InlineData(nameof(RecommendationsController.AcceptRecommendation), "recommendations.student.update_own")]
    [InlineData(nameof(RecommendationsController.DismissRecommendation), "recommendations.student.update_own")]
    [InlineData(nameof(RecommendationsController.GetActiveRecommendation), "recommendations.student.read_own")]
    [InlineData(nameof(RecommendationsController.GetActiveLearningPath), "recommendations.student.read_own")]
    public void Endpoint_RequiresExactPermissionPolicy(string methodName, string expectedPolicy)
    {
        var method = typeof(RecommendationsController).GetMethod(methodName);
        Assert.NotNull(method);
        var authAttr = method.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(authAttr);
        Assert.Equal(expectedPolicy, authAttr.Policy);
    }

    [Fact]
    public async Task Routing_UInt64GreaterThanLongMaxValue_RoutesCorrectlyViaAspNetPipeline()
    {
        ulong largeId = ((ulong)long.MaxValue) + 1000ul; // Greater than long.MaxValue!

        var mockAcceptUseCase = new Mock<IAcceptRecommendationUseCase>();
        mockAcceptUseCase.Setup(u => u.ExecuteAsync(largeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecommendationUseCaseResult.Success(new RecommendationDto
            {
                RecommendationId = largeId,
                TopicNodeId = 1,
                TopicName = "Topic 1",
                Type = RecommendationType.TopicAndQuestion,
                Explanation = "Test",
                Status = RecommendationStatus.Accepted,
                GeneratedAt = _utcNow
            }));

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddControllers()
                        .AddApplicationPart(typeof(RecommendationsController).Assembly);
                    services.AddSingleton(mockAcceptUseCase.Object);
                    services.AddSingleton(Mock.Of<IGetActiveRecommendationUseCase>());
                    services.AddSingleton(Mock.Of<IDismissRecommendationUseCase>());
                    services.AddSingleton(Mock.Of<IGetActiveLearningPathUseCase>());
                    services.AddSingleton(TimeProvider.System);
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                            .RequireAuthenticatedUser()
                            .Build();
                        options.AddPolicy("recommendations.student.update_own", policy =>
                            policy.RequireAssertion(_ => true));
                        options.AddPolicy("recommendations.student.read_own", policy =>
                            policy.RequireAssertion(_ => true));
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            });

        using var host = await hostBuilder.StartAsync();
        var client = host.GetTestClient();

        var response = await client.PostAsync($"/api/v1/students/me/recommendation/{largeId}/accept", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        mockAcceptUseCase.Verify(u => u.ExecuteAsync(largeId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim("role", "Student")
            }, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
        }
    }
}
