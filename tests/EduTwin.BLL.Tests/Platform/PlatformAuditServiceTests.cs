using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Platform;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Platform;

public sealed class PlatformAuditServiceTests
{
    private static readonly Guid PlatformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
    private static readonly Guid PlatformAdminUserId = Guid.NewGuid();

    private static (EduTwinDbContext Db, Mock<ITenantContext> TenantContext, PlatformAuditService Service) CreateContext(
        Guid? callerCenterId = null,
        string? callerRole = null,
        Guid? callerUserId = null)
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var effectiveCenterId = callerCenterId ?? PlatformCenterId;
        var effectiveRole = callerRole ?? nameof(UserRole.PlatformAdmin);
        var effectiveUserId = callerUserId ?? PlatformAdminUserId;

        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        mockTenantContext.Setup(c => c.CenterId).Returns(effectiveCenterId);
        mockTenantContext.Setup(c => c.Role).Returns(effectiveRole);
        mockTenantContext.Setup(c => c.UserId).Returns(effectiveUserId);

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(effectiveCenterId);

        var db = new EduTwinDbContext(options, mockAccessor.Object);
        var service = new PlatformAuditService(db, mockTenantContext.Object);

        return (db, mockTenantContext, service);
    }

    [Fact]
    public async Task ListAuditLogs_CallerNotPlatformAdmin_ReturnsForbiddenResource()
    {
        var customerCenterId = Guid.NewGuid();
        var (db, _, service) = CreateContext(customerCenterId, nameof(UserRole.CenterManager));
        using (db)
        {
            var result = await service.ListAuditLogsAsync(new PlatformAuditQuery());

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
        }
    }

    [Fact]
    public async Task GetAuditLogById_CallerNotPlatformAdmin_ReturnsForbiddenResource()
    {
        var customerCenterId = Guid.NewGuid();
        var (db, _, service) = CreateContext(customerCenterId, nameof(UserRole.CenterManager));
        using (db)
        {
            var result = await service.GetAuditLogByIdAsync(1);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ListAuditLogs_InvalidPageOrDateRange_ReturnsValidationFailed()
    {
        var (db, _, service) = CreateContext();
        using (db)
        {
            var pageResult = await service.ListAuditLogsAsync(new PlatformAuditQuery { Page = 0 });
            Assert.False(pageResult.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, pageResult.ErrorCode);

            var now = DateTime.UtcNow;
            var dateResult = await service.ListAuditLogsAsync(new PlatformAuditQuery
            {
                FromUtc = now,
                ToUtc = now.AddHours(-1)
            });
            Assert.False(dateResult.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, dateResult.ErrorCode);
        }
    }

    [Fact]
    public async Task ListAuditLogs_ScopingInvariant_ExcludesTenantInternalAudits()
    {
        var (db, _, service) = CreateContext();
        using (db)
        {
            var customerCenterId = Guid.NewGuid();

            // 1. Platform admin operational audit log
            db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
            {
                AuthorizationAuditId = 101,
                CenterId = PlatformCenterId,
                TargetCenterId = customerCenterId,
                ActorUserId = PlatformAdminUserId,
                ActionType = "CenterCreated",
                TargetType = "Center",
                TargetId = customerCenterId.ToString("D"),
                Reason = "Platform center provisioned",
                TraceId = "trace-plat-1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            });

            // 2. Ordinary tenant internal audit log
            db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
            {
                AuthorizationAuditId = 102,
                CenterId = customerCenterId,
                TargetCenterId = null,
                ActorUserId = Guid.NewGuid(),
                ActionType = "RoleAssigned",
                TargetType = "UserRole",
                TargetId = "role-1",
                Reason = "Internal tenant assignment",
                TraceId = "trace-cust-1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            });

            await db.SaveChangesAsync();

            var result = await service.ListAuditLogsAsync(new PlatformAuditQuery());

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data.TotalCount);
            Assert.Single(result.Data.Items);
            Assert.Equal("101", result.Data.Items[0].AuditId);
            Assert.Equal(PlatformCenterId, result.Data.Items[0].CenterId);
            Assert.Equal(customerCenterId, result.Data.Items[0].TargetCenterId);
        }
    }

    [Fact]
    public async Task ListAuditLogs_FilteringByActionTypeAndTargetCenterId_WorksProperly()
    {
        var (db, _, service) = CreateContext();
        using (db)
        {
            var centerA = Guid.NewGuid();
            var centerB = Guid.NewGuid();

            db.AuthorizationAuditLogs.AddRange(
                new AuthorizationAuditLog
                {
                    AuthorizationAuditId = 201,
                    CenterId = PlatformCenterId,
                    TargetCenterId = centerA,
                    ActorUserId = PlatformAdminUserId,
                    ActionType = "CenterCreated",
                    TargetType = "Center",
                    TargetId = centerA.ToString("D"),
                    Reason = "Center A provisioned",
                    TraceId = "trace-201",
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                },
                new AuthorizationAuditLog
                {
                    AuthorizationAuditId = 202,
                    CenterId = PlatformCenterId,
                    TargetCenterId = centerA,
                    ActorUserId = PlatformAdminUserId,
                    ActionType = "CenterManagerPasswordReset",
                    TargetType = "CenterManager",
                    TargetId = $"{centerA:D}:user-1",
                    Reason = "Password reset for manager A",
                    TraceId = "trace-202",
                    CreatedAt = DateTime.UtcNow.AddHours(-1)
                },
                new AuthorizationAuditLog
                {
                    AuthorizationAuditId = 203,
                    CenterId = PlatformCenterId,
                    TargetCenterId = centerB,
                    ActorUserId = PlatformAdminUserId,
                    ActionType = "CenterCreated",
                    TargetType = "Center",
                    TargetId = centerB.ToString("D"),
                    Reason = "Center B provisioned",
                    TraceId = "trace-203",
                    CreatedAt = DateTime.UtcNow
                }
            );

            await db.SaveChangesAsync();

            // Filter by targetCenterId = centerA
            var filterCenterResult = await service.ListAuditLogsAsync(new PlatformAuditQuery
            {
                TargetCenterId = centerA
            });
            Assert.True(filterCenterResult.IsSuccess);
            Assert.Equal(2, filterCenterResult.Data!.TotalCount);

            // Filter by targetCenterId = centerA AND actionType = CenterManagerPasswordReset
            var filterBothResult = await service.ListAuditLogsAsync(new PlatformAuditQuery
            {
                TargetCenterId = centerA,
                ActionType = "CenterManagerPasswordReset"
            });
            Assert.True(filterBothResult.IsSuccess);
            Assert.Equal(1, filterBothResult.Data!.TotalCount);
            Assert.Equal("202", filterBothResult.Data.Items[0].AuditId);

            // Search by keyword "Manager A"
            var searchResult = await service.ListAuditLogsAsync(new PlatformAuditQuery
            {
                Search = "manager A"
            });
            Assert.True(searchResult.IsSuccess);
            Assert.Equal(1, searchResult.Data!.TotalCount);
            Assert.Equal("202", searchResult.Data.Items[0].AuditId);
        }
    }

    [Fact]
    public async Task GetAuditLogById_WhenBelongsToCustomerCenter_ReturnsNotFound()
    {
        var (db, _, service) = CreateContext();
        using (db)
        {
            var customerCenterId = Guid.NewGuid();
            db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
            {
                AuthorizationAuditId = 301,
                CenterId = customerCenterId,
                ActorUserId = Guid.NewGuid(),
                ActionType = "RoleAssigned",
                TargetType = "UserRole",
                TargetId = "role-1",
                Reason = "Private log",
                TraceId = "trace-301",
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            var result = await service.GetAuditLogByIdAsync(301);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        }
    }

    [Fact]
    public void SanitizeAuditJson_RedactsSensitiveKeysTokensAndEducationalData()
    {
        var dirtyJson = JsonSerializer.Serialize(new
        {
            CenterId = Guid.NewGuid(),
            CenterCode = "CENTER_ALPHA",
            Password = "SuperSecretPassword123!",
            PasswordHash = "AQAAAAIAAYagAAAAEGK...",
            Token = "eyJhGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            AuthVersion = 5,
            ReasoningAnalysis = "Student chose B because of miscalculation",
            AwardedScore = 10.0,
            SafeMetadata = "Valid Operational Info"
        });

        var sanitized = PlatformAuditService.SanitizeAuditJson(dirtyJson);

        Assert.NotNull(sanitized);
        var jsonString = sanitized.Value.GetRawText();

        Assert.Contains("CENTER_ALPHA", jsonString);
        Assert.Contains("SafeMetadata", jsonString);
        Assert.Contains("Valid Operational Info", jsonString);

        // Disallowed sensitive keys must be excluded
        Assert.DoesNotContain("SuperSecretPassword123!", jsonString);
        Assert.DoesNotContain("AQAAAAIAAYagAAAAEGK...", jsonString);
        Assert.DoesNotContain("eyJhGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", jsonString);
        Assert.DoesNotContain("PasswordHash", jsonString);
        Assert.DoesNotContain("Password", jsonString);
        Assert.DoesNotContain("AuthVersion", jsonString);
        Assert.DoesNotContain("ReasoningAnalysis", jsonString);
        Assert.DoesNotContain("AwardedScore", jsonString);
    }

    [Fact]
    public async Task ListAuditLogs_CenterMetadataUpdatedFromCustomerCenter_IncludesAuditAndActorUsernameAndTargetCenterDetails()
    {
        var (db, _, service) = CreateContext();
        using (db)
        {
            var customerCenterId = Guid.NewGuid();
            var managerUserId = Guid.NewGuid();

            var center = new Center
            {
                CenterId = customerCenterId,
                CenterCode = "HA_NOI_01",
                CenterName = "Trung tâm Hà Nội Alpha",
                Status = EduTwin.Contracts.Organization.CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Centers.Add(center);

            var manager = new User
            {
                CenterId = customerCenterId,
                UserId = managerUserId,
                Username = "manager_hanoi",
                RoleName = UserRole.CenterManager,
                DisplayName = "Quản lý Hà Nội",
                PasswordHash = "hash",
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(manager);

            db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
            {
                AuthorizationAuditId = 501,
                CenterId = customerCenterId,
                TargetCenterId = customerCenterId,
                ActorUserId = managerUserId,
                ActionType = "CenterMetadataUpdated",
                TargetType = "Center",
                TargetId = customerCenterId.ToString("D"),
                Reason = "Cập nhật tên trung tâm",
                TraceId = "trace-501",
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            var result = await service.ListAuditLogsAsync(new PlatformAuditQuery());

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            var item = Assert.Single(result.Data.Items);
            Assert.Equal("501", item.AuditId);
            Assert.Equal("manager_hanoi", item.ActorUsername);
            Assert.Equal("HA_NOI_01", item.TargetCenterCode);
            Assert.Equal("Trung tâm Hà Nội Alpha", item.TargetCenterName);

            // Verify GetAuditLogByIdAsync also retrieves it with details
            var detailResult = await service.GetAuditLogByIdAsync(501);
            Assert.True(detailResult.IsSuccess);
            Assert.NotNull(detailResult.Data);
            Assert.Equal("manager_hanoi", detailResult.Data.ActorUsername);
            Assert.Equal("HA_NOI_01", detailResult.Data.TargetCenterCode);
            Assert.Equal("Trung tâm Hà Nội Alpha", detailResult.Data.TargetCenterName);
        }
    }
}
