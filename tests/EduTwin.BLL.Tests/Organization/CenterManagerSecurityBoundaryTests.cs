using System;
using System.Collections.Generic;
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
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.BLL.Platform;
using EduTwin.BLL.Dashboards;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
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

namespace EduTwin.BLL.Tests.Organization;

public sealed class CenterManagerSecurityBoundaryTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly Guid _customerCenterId = Guid.NewGuid();
    private readonly Guid _centerManagerUserId = Guid.NewGuid();
    private static readonly DateTime FixedUtcNow = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    public CenterManagerSecurityBoundaryTests()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddHttpContextAccessor();
                    services.AddControllers()
                        .AddApplicationPart(typeof(PlatformCentersController).Assembly);

                    // Mock platform and organization services
                    services.AddSingleton(Mock.Of<IPlatformCenterService>());
                    services.AddSingleton(Mock.Of<IPlatformAuditService>());
                    services.AddSingleton(Mock.Of<IPlatformMeService>());

                    services.AddSingleton(Mock.Of<IGetCenterProfileUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateCenterProfileUseCase>());
                    services.AddSingleton(Mock.Of<IGetCenterDashboardUseCase>());

                    services.AddSingleton(Mock.Of<IListTeachersUseCase>());
                    services.AddSingleton(Mock.Of<IGetTeacherUseCase>());
                    services.AddSingleton(Mock.Of<ICreateTeacherUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateTeacherUseCase>());
                    services.AddSingleton(Mock.Of<IDeleteTeacherUseCase>());

                    services.AddSingleton(Mock.Of<IListStudentsUseCase>());
                    services.AddSingleton(Mock.Of<IGetStudentUseCase>());
                    services.AddSingleton(Mock.Of<ICreateStudentUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateStudentUseCase>());

                    services.AddSingleton(Mock.Of<IListClassesUseCase>());
                    services.AddSingleton(Mock.Of<IGetClassUseCase>());
                    services.AddSingleton(Mock.Of<ICreateClassUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateClassUseCase>());

                    services.AddSingleton(Mock.Of<IListPermissionsUseCase>());
                    services.AddSingleton(Mock.Of<IListAuthorizationRolesUseCase>());
                    services.AddSingleton(Mock.Of<IGetAuthorizationRoleUseCase>());
                    services.AddSingleton(Mock.Of<ICreateAuthorizationRoleUseCase>());
                    services.AddSingleton(Mock.Of<IUpdateAuthorizationRoleUseCase>());
                    services.AddSingleton(Mock.Of<IReplaceRolePermissionsUseCase>());
                    services.AddSingleton(Mock.Of<IGetUserAuthorizationUseCase>());
                    services.AddSingleton(Mock.Of<IReplaceUserRolesUseCase>());
                    services.AddSingleton(Mock.Of<IListAuthorizationAuditUseCase>());

                    var mockTimeProvider = new Mock<TimeProvider>();
                    mockTimeProvider
                        .Setup(t => t.GetUtcNow())
                        .Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));
                    services.AddSingleton(mockTimeProvider.Object);

                    // Authentication setup
                    services.AddAuthentication("TestPersonaScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestPersonaAuthHandler>(
                            "TestPersonaScheme", _ => { });

                    // Evaluator setup based on persona header
                    var mockPermissionEvaluator = new Mock<IPermissionEvaluator>();
                    mockPermissionEvaluator
                        .Setup(e => e.HasPermissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((string code, CancellationToken _) =>
                        {
                            var httpContext = services.BuildServiceProvider()
                                .GetRequiredService<IHttpContextAccessor>().HttpContext;
                            var persona = httpContext?.Request.Headers["X-Test-Persona"].ToString();
                            if (string.IsNullOrEmpty(persona)) persona = "CenterManager";

                            if (persona == "CenterManager")
                            {
                                // CenterManager has normal tenant permissions but NEVER platform.*
                                return !code.StartsWith("platform.", StringComparison.OrdinalIgnoreCase);
                            }
                            if (persona == "Teacher")
                            {
                                // Teacher only has scoped teacher permissions
                                return code == "dashboards.teacher.read_scoped" ||
                                       code == "organization.teachers.read_scoped" ||
                                       code == "twin.student.read_scoped";
                            }
                            if (persona == "RestrictedManager")
                            {
                                // CenterManager with restricted permissions (e.g. read only, cannot create/manage)
                                return code == "organization.center.read" ||
                                       code == "dashboards.center.read" ||
                                       code == "organization.teachers.read";
                            }

                            return false;
                        });
                    services.AddSingleton(mockPermissionEvaluator.Object);

                    // Authorization setup
                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new AuthorizationPolicyBuilder("TestPersonaScheme")
                            .RequireAuthenticatedUser()
                            .Build();

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

    private void SetPersona(string persona)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Persona");
        _client.DefaultRequestHeaders.Add("X-Test-Persona", persona);
    }

    private static async Task AssertProblemDetailsForbiddenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.Status);
        Assert.Equal(ErrorCodes.AuthPermissionRequired, problem.Extensions["errorCode"]?.ToString());
    }

    // -----------------------------------------------------------------------
    // B1. CenterManager Denial Matrix (Locked out of all /api/v1/platform/*)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CenterManager_AccessPlatformCentersList_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.GetAsync("/api/v1/platform/centers");
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformCreateCenter_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PostAsJsonAsync("/api/v1/platform/centers", new { centerCode = "TEST", centerName = "Test" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformPatchCenterMetadata_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PatchAsJsonAsync($"/api/v1/platform/centers/{Guid.NewGuid()}", new { centerName = "New" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformPatchCenterStatus_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PatchAsJsonAsync($"/api/v1/platform/centers/{Guid.NewGuid()}/status", new { status = "Suspended", reason = "Test reason" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformListManagers_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.GetAsync($"/api/v1/platform/centers/{Guid.NewGuid()}/managers");
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformCreateManager_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PostAsJsonAsync($"/api/v1/platform/centers/{Guid.NewGuid()}/managers", new { username = "mgr" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformUpdateManagerStatus_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PatchAsJsonAsync($"/api/v1/platform/centers/{Guid.NewGuid()}/managers/{Guid.NewGuid()}/status", new { status = "Locked" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformMakePrimaryManager_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PostAsJsonAsync($"/api/v1/platform/centers/{Guid.NewGuid()}/managers/{Guid.NewGuid()}/make-primary", new { });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformResetManagerPassword_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PostAsJsonAsync($"/api/v1/platform/centers/{Guid.NewGuid()}/managers/{Guid.NewGuid()}/reset-password", new { newPassword = "Pass" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformAuditLogsList_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.GetAsync("/api/v1/platform/audit-logs");
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformAuditLogDetail_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.GetAsync("/api/v1/platform/audit-logs/12345");
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformMeSecurity_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.GetAsync("/api/v1/platform/me/security");
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformMeChangePassword_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PostAsJsonAsync("/api/v1/platform/me/change-password", new { currentPassword = "Old", newPassword = "New" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task CenterManager_AccessPlatformMeRevokeSessions_Returns403Forbidden()
    {
        SetPersona("CenterManager");
        var response = await _client.PostAsJsonAsync("/api/v1/platform/me/revoke-sessions", new { });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    // -----------------------------------------------------------------------
    // B2. Teacher / CenterManager Boundary Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Teacher_AccessCenterDashboard_Returns403Forbidden()
    {
        SetPersona("Teacher");
        var response = await _client.GetAsync("/api/v1/centers/me/dashboard");
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Teacher_AccessPatchCenterProfile_Returns403Forbidden()
    {
        SetPersona("Teacher");
        var response = await _client.PatchAsJsonAsync("/api/v1/centers/me", new { centerName = "Updated Center" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Teacher_CreateTeacher_Returns403Forbidden()
    {
        SetPersona("Teacher");
        var response = await _client.PostAsJsonAsync("/api/v1/teachers", new { username = "teacher2" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Teacher_CreateStudent_Returns403Forbidden()
    {
        SetPersona("Teacher");
        var response = await _client.PostAsJsonAsync("/api/v1/students", new { username = "student2" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task Teacher_AccessAuthorizationRoles_Returns403Forbidden()
    {
        SetPersona("Teacher");
        var response = await _client.GetAsync("/api/v1/authorization/roles");
        await AssertProblemDetailsForbiddenAsync(response);
    }

    // -----------------------------------------------------------------------
    // B3. Custom CenterManager Role Boundary (Revoked Permission -> 403)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RestrictedCenterManager_CreateTeacher_WithoutPermission_Returns403Forbidden()
    {
        SetPersona("RestrictedManager");
        var response = await _client.PostAsJsonAsync("/api/v1/teachers", new { username = "new_teacher" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    [Fact]
    public async Task RestrictedCenterManager_PatchCenterProfile_WithoutPermission_Returns403Forbidden()
    {
        SetPersona("RestrictedManager");
        var response = await _client.PatchAsJsonAsync("/api/v1/centers/me", new { centerName = "New Name" });
        await AssertProblemDetailsForbiddenAsync(response);
    }

    // -----------------------------------------------------------------------
    // B4. Tenant Isolation / Cross-Tenant Fail-Closed Tests (In-Memory DB)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TenantIsolation_CrossTenantTeacher_ReturnsOwnershipNotFound()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerA);

        using var db = new EduTwinDbContext(options, mockAccessor.Object);

        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        mockTenantContext.Setup(c => c.CenterId).Returns(centerA);
        mockTenantContext.Setup(c => c.UserId).Returns(_centerManagerUserId);
        mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        // Seed teacher in Center B
        var teacherInB = new Teacher
        {
            TeacherId = Guid.NewGuid(),
            CenterId = centerB,
            IsDeleted = false,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        };
        db.Teachers.Add(teacherInB);
        await db.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(db, mockTenantContext.Object);
        var decision = await guard.CheckTeacherAccessAsync(teacherInB.TeacherId, default);

        Assert.Equal(OwnershipDecision.NotFound, decision);
    }

    [Fact]
    public async Task TenantIsolation_CrossTenantStudent_ReturnsOwnershipNotFound()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerA);

        using var db = new EduTwinDbContext(options, mockAccessor.Object);

        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        mockTenantContext.Setup(c => c.CenterId).Returns(centerA);
        mockTenantContext.Setup(c => c.UserId).Returns(_centerManagerUserId);
        mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        // Seed student in Center B
        var studentInB = new Student
        {
            StudentId = Guid.NewGuid(),
            CenterId = centerB,
            FullName = "Student in B",
            IsDeleted = false,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        };
        db.Students.Add(studentInB);
        await db.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(db, mockTenantContext.Object);
        var decision = await guard.CheckStudentAccessAsync(studentInB.StudentId, default);

        Assert.Equal(OwnershipDecision.NotFound, decision);
    }

    [Fact]
    public async Task TenantIsolation_CrossTenantClass_ReturnsOwnershipNotFound()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerA);

        using var db = new EduTwinDbContext(options, mockAccessor.Object);

        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        mockTenantContext.Setup(c => c.CenterId).Returns(centerA);
        mockTenantContext.Setup(c => c.UserId).Returns(_centerManagerUserId);
        mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        // Seed class in Center B
        var classInB = new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = centerB,
            TeacherId = Guid.NewGuid(),
            ClassName = "Class in B",
            AcademicYear = "2026",
            Status = ClassStatus.Active,
            IsDeleted = false,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        };
        db.Classes.Add(classInB);
        await db.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(db, mockTenantContext.Object);
        var decision = await guard.CheckClassAccessAsync(classInB.ClassId, default);

        Assert.Equal(OwnershipDecision.NotFound, decision);
    }

    // -----------------------------------------------------------------------
    // B5. Account Type Compatibility & Invariant Checks
    // -----------------------------------------------------------------------

    [Fact]
    public void Catalog_AllPlatformPermissions_OnlyCompatibleWithPlatformAdmin()
    {
        var mappings = AuthorizationPermissionCatalog.CreateAccountTypeMappings();
        var permissions = AuthorizationPermissionCatalog.CreatePermissions();
        var platformPermissionIds = permissions
            .Where(p => p.PermissionCode.StartsWith("platform.", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.PermissionId)
            .ToHashSet();

        Assert.NotEmpty(platformPermissionIds);

        var platformMappings = mappings
            .Where(m => platformPermissionIds.Contains(m.PermissionId))
            .ToList();

        Assert.NotEmpty(platformMappings);
        foreach (var m in platformMappings)
        {
            Assert.Equal(UserRole.PlatformAdmin, m.AccountType);
        }
    }

    [Fact]
    public void Catalog_TenantAdminCorePermissions_ArePreservedAndNonEmpty()
    {
        var corePermissions = AuthorizationPermissionCatalog.TenantAdminCorePermissionsV1;
        Assert.NotEmpty(corePermissions);
        Assert.Contains("authorization.roles.read", corePermissions);
        Assert.Contains("authorization.roles.manage_permissions", corePermissions);
        Assert.Contains("authorization.user_roles.assign", corePermissions);

        var catalogPermissions = AuthorizationPermissionCatalog.CreatePermissions()
            .Select(p => p.PermissionCode)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var core in corePermissions)
        {
            Assert.Contains(core, catalogPermissions);
        }
    }

    [Fact]
    public async Task TenantAdministratorGuard_WhenAttemptingToRemoveLastAdmin_ReturnsFalse()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var centerId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        using var db = new EduTwinDbContext(options, mockAccessor.Object);

        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        mockTenantContext.Setup(c => c.CenterId).Returns(centerId);
        mockTenantContext.Setup(c => c.UserId).Returns(adminUserId);
        mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        // Seed sole active CenterManager
        db.Users.Add(new User
        {
            UserId = adminUserId,
            CenterId = centerId,
            Username = "sole_admin",
            DisplayName = "Sole Admin",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            AuthVersion = 1,
            PasswordHash = "hash",
            RowVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });

        // Seed active role
        db.AuthorizationRoles.Add(new AuthorizationRole
        {
            RoleId = adminRoleId,
            CenterId = centerId,
            RoleCode = "CM_ADMIN",
            RoleName = "Center Manager Admin",
            AccountType = UserRole.CenterManager,
            Status = AuthorizationRoleStatus.Active,
            IsSystemRole = true,
            RowVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });

        // Seed permissions in catalog
        var coreCodes = AuthorizationPermissionCatalog.TenantAdminCorePermissionsV1;
        var permissionEntities = new List<Permission>();
        foreach (var code in coreCodes)
        {
            var parts = code.Split('.');
            var p = new Permission
            {
                PermissionId = Guid.NewGuid(),
                PermissionCode = code,
                ModuleName = parts[0],
                ResourceName = parts[1],
                ActionName = parts[2],
                Description = code,
                IsSensitive = false,
                IsDelegable = true,
                Status = PermissionStatus.Active,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            db.Permissions.Add(p);
            permissionEntities.Add(p);

            db.RolePermissions.Add(new RolePermission
            {
                CenterId = centerId,
                RoleId = adminRoleId,
                PermissionId = p.PermissionId,
                AccountType = UserRole.CenterManager,
                GrantedAt = FixedUtcNow,
                GrantedByUserId = adminUserId
            });
        }

        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            CenterId = centerId,
            UserId = adminUserId,
            RoleId = adminRoleId,
            AccountType = UserRole.CenterManager,
            Status = UserRoleAssignmentStatus.Active,
            AssignedAt = FixedUtcNow,
            AssignedByUserId = adminUserId,
            RowVersion = 1
        });

        await db.SaveChangesAsync();

        var guard = new TenantAdministratorGuard(db, mockTenantContext.Object);

        // Deactivating the only admin role leaves 0 administrators with core permissions
        var canDeactivateRole = await guard.HasAdministratorAfterAsync(
            changedRoleId: adminRoleId,
            changedRoleActive: false);

        Assert.False(canDeactivateRole);

        // Stripping core permissions leaves 0 administrators with core permissions
        var canStripPermissions = await guard.HasAdministratorAfterAsync(
            changedRoleId: adminRoleId,
            changedRoleActive: true,
            replacementPermissionIds: Array.Empty<Guid>());

        Assert.False(canStripPermissions);

        // Leaving it as-is returns true
        var canKeepAsIs = await guard.HasAdministratorAfterAsync();
        Assert.True(canKeepAsIs);
    }

    private class TestPersonaAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestPersonaAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var persona = Request.Headers["X-Test-Persona"].ToString();
            if (string.IsNullOrEmpty(persona)) persona = "CenterManager";

            var role = persona switch
            {
                "Teacher" => nameof(UserRole.Teacher),
                _ => nameof(UserRole.CenterManager)
            };

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001"),
                new Claim("sub", "00000000-0000-0000-0000-000000000001"),
                new Claim("center_id", Guid.NewGuid().ToString("D")),
                new Claim("role", role),
                new Claim("auth_version", "1")
            };

            var identity = new ClaimsIdentity(claims, "TestPersonaScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestPersonaScheme");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
