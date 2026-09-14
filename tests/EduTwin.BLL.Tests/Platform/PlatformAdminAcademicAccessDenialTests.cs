using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.Controllers;
using EduTwin.API.Security;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.AssessmentAndReasoning.Polling;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.Dashboards;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.BLL.Platform;
using EduTwin.BLL.Recommendations.UseCases;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Platform;

public sealed class PlatformAdminAcademicAccessDenialTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly Guid _platformAdminUserId = Guid.NewGuid();
    private static readonly DateTime FixedUtcNow = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

    public PlatformAdminAcademicAccessDenialTests()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddControllers()
                        .AddApplicationPart(typeof(LearningController).Assembly);

                    // Register mocked use cases so controller activation succeeds
                    services.AddSingleton(Mock.Of<ISubmitAttemptUseCase>());
                    services.AddSingleton(Mock.Of<IListAttemptsUseCase>());
                    services.AddSingleton(Mock.Of<IGetAnalysisJobStatusUseCase>());
                    services.AddSingleton(Mock.Of<IGetNextQuestionUseCase>());
                    services.AddSingleton(Mock.Of<IGetAttemptFeedbackUseCase>());

                    services.AddSingleton(Mock.Of<IPrepareAttemptAttachmentUploadUseCase>());
                    services.AddSingleton(Mock.Of<IGetAttemptAttachmentUseCase>());
                    services.AddSingleton(Mock.Of<IAttemptAttachmentStorage>());

                    services.AddSingleton(Mock.Of<IListStudentsUseCase>());
                    services.AddSingleton(Mock.Of<IGetStudentUseCase>());
                    services.AddSingleton(Mock.Of<ICreateStudentUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateStudentUseCase>());
                    services.AddSingleton(Mock.Of<IUpsertStudentSubjectGoalUseCase>());
                    services.AddSingleton(Mock.Of<IListStudentSubjectGoalsUseCase>());
                    services.AddSingleton(Mock.Of<IGetStudentDashboardUseCase>());
                    services.AddSingleton(Mock.Of<IGetStudentTwinUseCase>());
                    services.AddSingleton(Mock.Of<IGetStudentTwinHistoryUseCase>());

                    services.AddSingleton(Mock.Of<IListTeachersUseCase>());
                    services.AddSingleton(Mock.Of<IGetTeacherUseCase>());
                    services.AddSingleton(Mock.Of<ICreateTeacherUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateTeacherUseCase>());
                    services.AddSingleton(Mock.Of<IDeleteTeacherUseCase>());

                    services.AddSingleton(Mock.Of<IListTeacherReviewQueueUseCase>());
                    services.AddSingleton(Mock.Of<ITeacherOverrideUseCase>());
                    services.AddSingleton(Mock.Of<IGetTeacherStudentTwinUseCase>());

                    services.AddSingleton(Mock.Of<IGetActiveRecommendationUseCase>());
                    services.AddSingleton(Mock.Of<IAcceptRecommendationUseCase>());
                    services.AddSingleton(Mock.Of<IDismissRecommendationUseCase>());
                    services.AddSingleton(Mock.Of<IGetActiveLearningPathUseCase>());

                    services.AddSingleton(Mock.Of<ICreateAssignmentUseCase>());
                    services.AddSingleton(Mock.Of<IGetAssignmentUseCase>());
                    services.AddSingleton(Mock.Of<IListAssignmentsUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateAssignmentUseCase>());
                    services.AddSingleton(Mock.Of<IPublishAssignmentUseCase>());
                    services.AddSingleton(Mock.Of<ICloseAssignmentUseCase>());
                    services.AddSingleton(Mock.Of<IGetAssignmentProgressUseCase>());
                    services.AddSingleton(Mock.Of<IListStudentAssignmentsUseCase>());
                    services.AddSingleton(Mock.Of<IGetStudentAssignmentUseCase>());

                    services.AddSingleton(Mock.Of<ICreateClassUseCase>());
                    services.AddSingleton(Mock.Of<IGetClassUseCase>());
                    services.AddSingleton(Mock.Of<IListClassesUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateClassUseCase>());
                    services.AddSingleton(Mock.Of<IGetClassDashboardUseCase>());

                    services.AddSingleton(Mock.Of<IGetCenterProfileUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateCenterProfileUseCase>());
                    services.AddSingleton(Mock.Of<IGetCenterDashboardUseCase>());

                    var mockTimeProvider = new Mock<TimeProvider>();
                    mockTimeProvider
                        .Setup(t => t.GetUtcNow())
                        .Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));
                    services.AddSingleton(mockTimeProvider.Object);

                    // Authentication setup: authenticate as PlatformAdmin in Root Tenant PLATFORM
                    services.AddAuthentication("PlatformAdminScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestPlatformAdminAuthHandler>(
                            "PlatformAdminScheme", _ => { });

                    // Evaluator that strictly models PlatformAdmin capability: ONLY platform.* permissions
                    var mockPermissionEvaluator = new Mock<IPermissionEvaluator>();
                    mockPermissionEvaluator
                        .Setup(e => e.HasPermissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((string code, CancellationToken _) => code.StartsWith("platform.", StringComparison.OrdinalIgnoreCase));
                    services.AddSingleton(mockPermissionEvaluator.Object);

                    // Authorization setup: load real permission policies & composite policies
                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new AuthorizationPolicyBuilder("PlatformAdminScheme")
                            .RequireAuthenticatedUser()
                            .Build();

                        options.AddPolicy(
                            AuthorizationPolicies.StudentOnly,
                            policy => policy.RequireClaim("role", nameof(UserRole.Student)));

                        foreach (var perm in AuthorizationPermissionCatalog.CreatePermissions())
                        {
                            options.AddPolicy(
                                perm.PermissionCode,
                                policy => policy.AddRequirements(new PermissionRequirement(perm.PermissionCode)));
                        }

                        foreach (var composite in CompositePermissionPolicies.GetAnyPermissionPolicies())
                        {
                            options.AddPolicy(
                                composite.Key,
                                policy => policy.AddRequirements(new AnyPermissionRequirement(composite.Value)));
                        }
                    });

                    services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
                    services.AddScoped<IAuthorizationHandler, AnyPermissionAuthorizationHandler>();
                    services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();
                });

                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            });

        _host = hostBuilder.Start();
        _client = _host.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
    }

    // -------------------------------------------------------------
    // HTTP API Academic Access Denial Tests (Fail-Closed 403 Matrix)
    // -------------------------------------------------------------

    [Fact]
    public async Task LearningAttempts_Submit_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/learning/attempts", new
        {
            assignmentId = Guid.NewGuid(),
            questionId = 101,
            selectedOptionId = (ulong?)1,
            reasoningText = "My student reasoning"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task LearningAttempts_List_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync("/api/v1/learning/attempts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task LearningAttempts_Feedback_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync("/api/v1/learning/attempts/1/feedback");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task LearningAttempts_AttachmentDownload_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync("/api/v1/learning/attempts/1/attachment");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task LearningAttempts_PrepareUpload_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        using var content = new MultipartFormDataContent("test-boundary");
        var response = await _client.PostAsync("/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task LearningAnalysisJobs_Status_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync("/api/v1/learning/analysis-jobs/job-12345");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task LearningNextQuestion_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/learning/next-question?subjectId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task StudentTwin_Get_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/students/me/twin?subjectId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task StudentTwinHistory_Get_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/students/me/twin/history?subjectId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task StudentDashboard_Get_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/students/me/dashboard?subjectId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task StudentGoals_List_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/students/{Guid.NewGuid()}/goals");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Recommendations_GetActive_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync("/api/v1/students/me/recommendation");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task TeacherReviewQueue_List_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync("/api/v1/teachers/me/review-queue");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task TeacherOverride_Post_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/teachers/me/reasoning-analyses/1/override", new
        {
            overrideScore = 85.0m,
            reason = "Teacher manual override validation"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task TeacherScopedStudentTwin_Get_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/teachers/me/students/{Guid.NewGuid()}/twin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Assignments_GetDetail_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/assignments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Assignments_GetProgress_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/assignments/{Guid.NewGuid()}/progress");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Dashboards_Class_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync($"/api/v1/classes/{Guid.NewGuid()}/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Dashboards_Center_WhenCalledByPlatformAdmin_ReturnsForbidden403()
    {
        var response = await _client.GetAsync("/api/v1/centers/me/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertProblemDetailsForbiddenAsync(response);
    }

    // -------------------------------------------------------------
    // Privilege Escalation & Catalog Boundary Tests
    // -------------------------------------------------------------

    [Fact]
    public void Catalog_PlatformAdmin_HasZeroAcademicPermissions()
    {
        var mappings = AuthorizationPermissionCatalog.CreateAccountTypeMappings();
        var platformAdminPermissionIds = mappings
            .Where(m => m.AccountType == UserRole.PlatformAdmin)
            .Select(m => m.PermissionId)
            .ToHashSet();

        var allPermissions = AuthorizationPermissionCatalog.CreatePermissions();
        var platformAdminPermissions = allPermissions
            .Where(p => platformAdminPermissionIds.Contains(p.PermissionId))
            .ToList();

        Assert.NotEmpty(platformAdminPermissions);

        // Invariant: PlatformAdmin ONLY possesses permissions in the "platform.*" namespace
        Assert.All(platformAdminPermissions, p =>
        {
            Assert.StartsWith("platform.", p.PermissionCode, StringComparison.OrdinalIgnoreCase);
        });

        // Specific academic denial checks:
        Assert.DoesNotContain(platformAdminPermissions, p => p.PermissionCode.StartsWith("learning."));
        Assert.DoesNotContain(platformAdminPermissions, p => p.PermissionCode.StartsWith("twin."));
        Assert.DoesNotContain(platformAdminPermissions, p => p.PermissionCode.StartsWith("recommendations."));
        Assert.DoesNotContain(platformAdminPermissions, p => p.PermissionCode.StartsWith("dashboards."));
        Assert.DoesNotContain(platformAdminPermissions, p => p.PermissionCode.StartsWith("assignments."));
        Assert.DoesNotContain(platformAdminPermissions, p => p.PermissionCode.StartsWith("curriculum."));
        Assert.DoesNotContain(platformAdminPermissions, p => p.PermissionCode.StartsWith("knowledge."));
    }

    [Fact]
    public async Task PlatformCenterService_ResetPassword_WhenTargetIsRootTenantPlatform_ReturnsForbiddenResource()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        mockTenantContext.Setup(c => c.CenterId).Returns(AuthorizationBootstrapper.ReservedPlatformCenterId);
        mockTenantContext.Setup(c => c.UserId).Returns(_platformAdminUserId);
        mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.PlatformAdmin));

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(AuthorizationBootstrapper.ReservedPlatformCenterId);

        using var db = new EduTwinDbContext(options, mockAccessor.Object);
        var mockTimeProvider = new Mock<TimeProvider>();
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));

        var bootstrapper = new AuthorizationBootstrapper(db, mockTimeProvider.Object);
        var mockPasswordHasher = new Mock<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        var service = new PlatformCenterService(db, mockTenantContext.Object, mockPasswordHasher.Object, bootstrapper, mockTimeProvider.Object);

        var request = new ResetCenterManagerPasswordRequest
        {
            NewPassword = "NewValidSecurePassword123!",
            ExpectedUserRowVersion = "1",
            Reason = "Operational password rotation reason"
        };

        // Attempting to use reset-manager endpoint on PLATFORM root tenant MUST return ForbiddenResource
        var result = await service.ResetCenterManagerPasswordAsync(
            AuthorizationBootstrapper.ReservedPlatformCenterId,
            _platformAdminUserId,
            request,
            "trace-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task PlatformCenterService_ResetPassword_WhenTargetUserIsNotCenterManager_ReturnsValidationFailed()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var customerCenterId = Guid.NewGuid();
        var teacherUserId = Guid.NewGuid();

        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        mockTenantContext.Setup(c => c.CenterId).Returns(AuthorizationBootstrapper.ReservedPlatformCenterId);
        mockTenantContext.Setup(c => c.UserId).Returns(_platformAdminUserId);
        mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.PlatformAdmin));

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(AuthorizationBootstrapper.ReservedPlatformCenterId);

        using var db = new EduTwinDbContext(options, mockAccessor.Object);

        db.Centers.Add(new Center
        {
            CenterId = customerCenterId,
            CenterCode = "CUST_TEST",
            CenterName = "Customer Test",
            Status = Contracts.Organization.CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        });

        // Add a Teacher user in this center
        db.Users.Add(new User
        {
            UserId = teacherUserId,
            CenterId = customerCenterId,
            Username = "teacher_test",
            DisplayName = "Teacher Test",
            RoleName = UserRole.Teacher, // Not CenterManager!
            Status = UserStatus.Active,
            AuthVersion = 1,
            PasswordHash = "hash",
            RowVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });
        await db.SaveChangesAsync();

        var mockTimeProvider = new Mock<TimeProvider>();
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));

        var bootstrapper = new AuthorizationBootstrapper(db, mockTimeProvider.Object);
        var mockPasswordHasher = new Mock<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        var service = new PlatformCenterService(db, mockTenantContext.Object, mockPasswordHasher.Object, bootstrapper, mockTimeProvider.Object);

        var request = new ResetCenterManagerPasswordRequest
        {
            NewPassword = "NewValidSecurePassword123!",
            ExpectedUserRowVersion = "1",
            Reason = "Operational password rotation reason"
        };

        // Reset password on a non-CenterManager user MUST fail with ResourceNotFound
        var result = await service.ResetCenterManagerPasswordAsync(
            customerCenterId,
            teacherUserId,
            request,
            "trace-2");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task PlatformCenterService_CreateCenter_WhenCenterCodeIsPlatform_ReturnsForbiddenResource()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        mockTenantContext.Setup(c => c.CenterId).Returns(AuthorizationBootstrapper.ReservedPlatformCenterId);
        mockTenantContext.Setup(c => c.UserId).Returns(_platformAdminUserId);
        mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.PlatformAdmin));

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(AuthorizationBootstrapper.ReservedPlatformCenterId);

        using var db = new EduTwinDbContext(options, mockAccessor.Object);
        var mockTimeProvider = new Mock<TimeProvider>();
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));

        var bootstrapper = new AuthorizationBootstrapper(db, mockTimeProvider.Object);
        var mockPasswordHasher = new Mock<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        var service = new PlatformCenterService(db, mockTenantContext.Object, mockPasswordHasher.Object, bootstrapper, mockTimeProvider.Object);

        var request = new CreatePlatformCenterRequest
        {
            CenterCode = "PLATFORM", // Forbidden keyword
            CenterName = "Fake Platform",
            Timezone = "Asia/Ho_Chi_Minh",
            InitialManagerUsername = "fake_manager",
            InitialManagerDisplayName = "Fake Manager",
            InitialManagerPassword = "InitialSecurePassword123!"
        };

        var result = await service.CreateCenterAsync(request, "trace-3");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    private static async Task AssertProblemDetailsForbiddenAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.Status);
        Assert.Equal(ErrorCodes.AuthPermissionRequired, problem.Extensions["errorCode"]?.ToString());
    }

    private class TestPlatformAdminAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestPlatformAdminAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000002"),
                new Claim("sub", "00000000-0000-0000-0000-000000000002"),
                new Claim("center_id", AuthorizationBootstrapper.ReservedPlatformCenterId.ToString("D")),
                new Claim("role", nameof(UserRole.PlatformAdmin)),
                new Claim("auth_version", "1")
            };

            var identity = new ClaimsIdentity(claims, "PlatformAdminScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "PlatformAdminScheme");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
