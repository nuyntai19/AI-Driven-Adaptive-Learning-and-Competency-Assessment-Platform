using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MySql.Data.MySqlClient;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Platform;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Xunit;

namespace EduTwin.BLL.Tests.Platform;

[Collection("MySqlDatabase")]
public sealed class PlatformMySqlIntegrationTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime FixedUtcNow = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
    private readonly Mock<TimeProvider> _mockTimeProvider;

    public PlatformMySqlIntegrationTests()
    {
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider
            .Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));
    }

    [MySqlIntegrationFact]
    public async Task CheckConstraints_PermitsPlatformAdmin_AndRejectsInvalidRolesOnLiveMySql()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var tenant = new TenantContext();
        await using var context = CreateContext(database.ConnectionString, tenant);

        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var validUserId = Guid.NewGuid();

        // 1. ck_users_role_name: PlatformAdmin accepted, invalid role rejected
        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO centers (center_id, center_code, center_name, status, timezone, created_at, updated_at, row_version)
              VALUES ({0}, 'PLATFORM', 'Platform Admin Center', 'Active', 'Asia/Ho_Chi_Minh', {1}, {1}, 1);",
            platformCenterId, FixedUtcNow);

        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO users (user_id, center_id, username, display_name, password_hash, role_name, status, auth_version, created_at, updated_at, row_version)
              VALUES ({0}, {1}, 'valid.platform.admin', 'Admin', 'hash', 'PlatformAdmin', 'Active', 1, {2}, {2}, 1);",
            validUserId, platformCenterId, FixedUtcNow);

        var userEx = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO users (user_id, center_id, username, display_name, password_hash, role_name, status, auth_version, created_at, updated_at, row_version)
                  VALUES ({0}, {1}, 'bad.user', 'Bad', 'hash', 'SuperAdmin', 'Active', 1, {2}, {2}, 1);",
                Guid.NewGuid(), platformCenterId, FixedUtcNow);
        });
        Assert.Contains("ck_users_role_name", userEx.Message, StringComparison.OrdinalIgnoreCase);

        // 2. ck_permission_account_types_account_type: PlatformAdmin accepted, invalid rejected
        var testPermId = Guid.NewGuid().ToString();
        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO permissions (permission_id, permission_code, action_name, resource_name, module_name, description, is_sensitive, is_delegable, status, created_at, updated_at)
              VALUES ({0}, 'test.dummy.perm', 'manage', 'Dummy', 'Test', 'Test description', 1, 0, 'Active', {1}, {1});",
            testPermId, FixedUtcNow);

        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO permission_account_types (permission_id, account_type, created_at)
              VALUES ({0}, 'PlatformAdmin', {1});",
            testPermId, FixedUtcNow);

        var permEx = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO permission_account_types (permission_id, account_type, created_at)
                  VALUES ({0}, 'SuperAdmin', {1});",
                testPermId, FixedUtcNow);
        });
        Assert.Contains("ck_permission_account_types_account_type", permEx.Message, StringComparison.OrdinalIgnoreCase);

        // 3. ck_roles_account_type: PlatformAdmin accepted, invalid rejected
        var validRoleId = Guid.NewGuid();
        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO roles (role_id, center_id, role_code, role_name, account_type, is_system_role, is_deleted, status, row_version, created_at, updated_at)
              VALUES ({0}, {1}, 'PLAT_ROLE', 'Platform Role', 'PlatformAdmin', 1, 0, 'Active', 1, {2}, {2});",
            validRoleId, platformCenterId, FixedUtcNow);

        var roleEx = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO roles (role_id, center_id, role_code, role_name, account_type, is_system_role, is_deleted, status, row_version, created_at, updated_at)
                  VALUES ({0}, {1}, 'BAD_ROLE', 'Bad Role', 'SuperAdmin', 1, 0, 'Active', 1, {2}, {2});",
                Guid.NewGuid(), platformCenterId, FixedUtcNow);
        });
        Assert.Contains("ck_roles_account_type", roleEx.Message, StringComparison.OrdinalIgnoreCase);

        // 4. ck_role_permissions_account_type: PlatformAdmin accepted, invalid rejected
        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO role_permissions (role_id, permission_id, center_id, account_type, granted_at, granted_by_user_id)
              VALUES ({0}, 'a39a542b-7b4f-55f6-aa8d-4f6f2073f852', {1}, 'PlatformAdmin', {2}, {3});",
            validRoleId, platformCenterId, FixedUtcNow, validUserId);

        var rolePermEx = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO role_permissions (role_id, permission_id, center_id, account_type, granted_at, granted_by_user_id)
                  VALUES ({0}, 'a39a542b-7b4f-55f6-aa8d-4f6f2073f852', {1}, 'SuperAdmin', {2}, {3});",
                validRoleId, platformCenterId, FixedUtcNow, validUserId);
        });
        Assert.Contains("ck_role_permissions_account_type", rolePermEx.Message, StringComparison.OrdinalIgnoreCase);

        // 5. ck_user_roles_account_type: PlatformAdmin accepted, invalid rejected
        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO user_roles (center_id, user_id, role_id, account_type, status, assigned_at, assigned_by_user_id, row_version)
              VALUES ({0}, {1}, {2}, 'PlatformAdmin', 'Active', {3}, {1}, 1);",
            platformCenterId, validUserId, validRoleId, FixedUtcNow);

        var userRoleEx = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO user_roles (center_id, user_id, role_id, account_type, status, assigned_at, assigned_by_user_id, row_version)
                  VALUES ({0}, {1}, {2}, 'SuperAdmin', 'Active', {3}, {1}, 1);",
                platformCenterId, validUserId, validRoleId, FixedUtcNow);
        });
        Assert.Contains("ck_user_roles_account_type", userRoleEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [MySqlIntegrationFact]
    public async Task PlatformAudit_NullTargetUserId_Succeeds_And_NonNullCrossTenantTarget_ThrowsFkError()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var tenant = new TenantContext();
        await using var context = CreateContext(database.ConnectionString, tenant);

        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var customerCenterId = Guid.NewGuid();
        var platformAdminUserId = Guid.NewGuid();
        var customerUserId = Guid.NewGuid();

        // Seed platform center and customer center
        context.Centers.AddRange(
            new Center
            {
                CenterId = platformCenterId,
                CenterCode = "PLATFORM",
                CenterName = "EduTwin Platform",
                Status = CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            },
            new Center
            {
                CenterId = customerCenterId,
                CenterCode = "CUST_001",
                CenterName = "Customer Center 1",
                Status = CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            }
        );

        // Seed platform admin in PLATFORM and customer user in CUST_001
        context.Users.AddRange(
            new User
            {
                UserId = platformAdminUserId,
                CenterId = platformCenterId,
                Username = "platform.admin",
                DisplayName = "Platform Admin",
                PasswordHash = "hash",
                RoleName = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            },
            new User
            {
                UserId = customerUserId,
                CenterId = customerCenterId,
                Username = "customer.manager",
                DisplayName = "Customer Manager",
                PasswordHash = "hash",
                RoleName = UserRole.CenterManager,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            }
        );

        await context.SaveChangesAsync();

        // Cross-tenant audit with TargetUserId = null MUST succeed
        var successfulAudit = new AuthorizationAuditLog
        {
            CenterId = platformCenterId,
            ActorUserId = platformAdminUserId,
            ActionType = "CenterCreated",
            TargetType = "Center",
            TargetId = customerCenterId.ToString("D"),
            TargetUserId = null,
            Reason = "Cross-tenant center provisioning.",
            TraceId = "trace-mysql-audit-ok",
            CreatedAt = FixedUtcNow
        };
        context.AuthorizationAuditLogs.Add(successfulAudit);
        await context.SaveChangesAsync();

        Assert.True(successfulAudit.AuthorizationAuditId > 0);

        // Cross-tenant audit with non-null TargetUserId pointing to customer tenant user MUST fail composite FK (center_id, target_user_id)
        var invalidFkAudit = new AuthorizationAuditLog
        {
            CenterId = platformCenterId,
            ActorUserId = platformAdminUserId,
            ActionType = "CenterCreated",
            TargetType = "Center",
            TargetId = customerCenterId.ToString("D"),
            TargetUserId = customerUserId, // Foreign tenant user!
            Reason = "Cross-tenant violation attempt.",
            TraceId = "trace-mysql-audit-fail",
            CreatedAt = FixedUtcNow
        };
        context.AuthorizationAuditLogs.Add(invalidFkAudit);

        var dbEx = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await context.SaveChangesAsync();
        });

        var innerMySql = dbEx.GetBaseException() as MySqlException;
        Assert.NotNull(innerMySql);
        Assert.Contains("fk_authorization_audit_logs_users_target", innerMySql.Message, StringComparison.OrdinalIgnoreCase);
    }

    [MySqlIntegrationFact]
    public async Task CreateCenterAsync_AtomicTransaction_PersistsCenterUserRoleAndAudit()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();

        // Seed platform center & admin user
        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.Add(new Center
            {
                CenterId = platformCenterId,
                CenterCode = "PLATFORM",
                CenterName = "Root Tenant",
                Status = CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            setupContext.Users.Add(new User
            {
                UserId = platformAdminUserId,
                CenterId = platformCenterId,
                Username = "root.admin",
                DisplayName = "Root Admin",
                PasswordHash = "hash",
                RoleName = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        // Initialize tenant context as PlatformAdmin
        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        await using var serviceContext = CreateContext(database.ConnectionString, callerContext);
        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper = new AuthorizationBootstrapper(serviceContext, _mockTimeProvider.Object);
        var service = new PlatformCenterService(
            serviceContext,
            callerContext,
            passwordHasher,
            authBootstrapper,
            _mockTimeProvider.Object);

        var request = new CreatePlatformCenterRequest
        {
            CenterCode = "NEW_CENTER",
            CenterName = "New Test Center",
            Timezone = "Asia/Ho_Chi_Minh",
            InitialManagerUsername = "new.manager",
            InitialManagerDisplayName = "New Manager",
            InitialManagerPassword = "StrongManagerPass123!"
        };

        var result = await service.CreateCenterAsync(request, "trace-mysql-create");

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var created = result.Data!;
        Assert.Equal("NEW_CENTER", created.CenterCode);
        Assert.Equal(CenterStatus.Active.ToString(), created.Status);

        // Verify direct relational MySQL state
        await using var verifyContext = CreateContext(database.ConnectionString, setupTenant);
        var center = await verifyContext.Centers.IgnoreQueryFilters().SingleAsync(c => c.CenterCode == "NEW_CENTER");
        Assert.Equal("New Test Center", center.CenterName);
        Assert.Equal(1u, center.RowVersion);

        var manager = await verifyContext.Users.IgnoreQueryFilters().SingleAsync(u => u.CenterId == center.CenterId);
        Assert.Equal("new.manager", manager.Username);
        Assert.Equal(UserRole.CenterManager, manager.RoleName);
        Assert.Equal(1u, manager.AuthVersion);

        var roleAssignment = await verifyContext.UserRoleAssignments.IgnoreQueryFilters().SingleAsync(ur => ur.UserId == manager.UserId);
        Assert.Equal(UserRole.CenterManager, roleAssignment.AccountType);

        var audit = await verifyContext.AuthorizationAuditLogs
            .IgnoreQueryFilters()
            .SingleAsync(a => a.TargetId == center.CenterId.ToString("D") && a.ActionType == "CenterCreated");
        Assert.Equal(platformCenterId, audit.CenterId);
        Assert.Equal(platformAdminUserId, audit.ActorUserId);
        Assert.Null(audit.TargetUserId);
        Assert.Equal("CenterCreated", audit.ActionType);
    }

    [MySqlIntegrationFact]
    public async Task UpdateCenterStatusAsync_ConcurrentUpdates_OneWins_OneReturnsConcurrencyConflict()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();
        var testCenterId = Guid.NewGuid();

        // Seed platform center, admin, and target center
        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.AddRange(
                new Center
                {
                    CenterId = platformCenterId,
                    CenterCode = "PLATFORM",
                    CenterName = "Root Tenant",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Center
                {
                    CenterId = testCenterId,
                    CenterCode = "CONC_CTR",
                    CenterName = "Concurrency Test Center",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );
            setupContext.Users.Add(new User
            {
                UserId = platformAdminUserId,
                CenterId = platformCenterId,
                Username = "root.admin",
                DisplayName = "Root Admin",
                PasswordHash = "hash",
                RoleName = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        var callerContext1 = new TenantContext();
        callerContext1.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);
        var callerContext2 = new TenantContext();
        callerContext2.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        await using var context1 = CreateContext(database.ConnectionString, callerContext1);
        await using var context2 = CreateContext(database.ConnectionString, callerContext2);

        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper1 = new AuthorizationBootstrapper(context1, _mockTimeProvider.Object);
        var authBootstrapper2 = new AuthorizationBootstrapper(context2, _mockTimeProvider.Object);

        var service1 = new PlatformCenterService(
            context1,
            callerContext1,
            passwordHasher,
            authBootstrapper1,
            _mockTimeProvider.Object);
        var service2 = new PlatformCenterService(
            context2,
            callerContext2,
            passwordHasher,
            authBootstrapper2,
            _mockTimeProvider.Object);

        var updateReq = new UpdatePlatformCenterStatusRequest
        {
            Status = CenterStatus.Suspended.ToString(),
            RowVersion = "1"
        };

        // Update with service1 succeeds, bumping RowVersion from 1 to 2
        var res1 = await service1.UpdateCenterStatusAsync(testCenterId, updateReq, "trace-conc-1");
        Assert.True(res1.IsSuccess);
        Assert.Equal("2", res1.Data!.RowVersion);

        // Attempt with service2 using stale expectedVersion = 1 MUST yield ConcurrencyConflict (409)
        var res2 = await service2.UpdateCenterStatusAsync(testCenterId, updateReq, "trace-conc-2");
        Assert.False(res2.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, res2.ErrorCode);
    }

    [MySqlIntegrationFact]
    public async Task CreateCenterAsync_ConcurrentDuplicateCenterCode_ReturnsDuplicateResource()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();

        // Seed platform center & admin
        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.Add(new Center
            {
                CenterId = platformCenterId,
                CenterCode = "PLATFORM",
                CenterName = "Root Tenant",
                Status = CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            setupContext.Users.Add(new User
            {
                UserId = platformAdminUserId,
                CenterId = platformCenterId,
                Username = "root.admin",
                DisplayName = "Root Admin",
                PasswordHash = "hash",
                RoleName = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        await using var context = CreateContext(database.ConnectionString, callerContext);
        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper = new AuthorizationBootstrapper(context, _mockTimeProvider.Object);
        var service = new PlatformCenterService(
            context,
            callerContext,
            passwordHasher,
            authBootstrapper,
            _mockTimeProvider.Object);

        var request = new CreatePlatformCenterRequest
        {
            CenterCode = "DUPLICATE_CODE",
            CenterName = "Original Center",
            Timezone = "Asia/Ho_Chi_Minh",
            InitialManagerUsername = "mgr.orig",
            InitialManagerDisplayName = "Original Manager",
            InitialManagerPassword = "Password123456!"
        };

        var firstResult = await service.CreateCenterAsync(request, "trace-dup-1");
        Assert.True(firstResult.IsSuccess);

        // Second call with identical center code MUST fail with DuplicateResource (409)
        var secondResult = await service.CreateCenterAsync(request, "trace-dup-2");
        Assert.False(secondResult.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, secondResult.ErrorCode);
    }

    [MySqlIntegrationFact]
    public async Task UpdateCenterStatusAsync_RelationalOCCRace_WhenDbUpdateConcurrencyExceptionThrown_ReturnsConcurrencyConflict()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();
        var testCenterId = Guid.NewGuid();

        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.AddRange(
                new Center
                {
                    CenterId = platformCenterId,
                    CenterCode = "PLATFORM",
                    CenterName = "Root Tenant",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Center
                {
                    CenterId = testCenterId,
                    CenterCode = "RACE_CTR",
                    CenterName = "Race Target Center",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );
            setupContext.Users.Add(new User
            {
                UserId = platformAdminUserId,
                CenterId = platformCenterId,
                Username = "root.admin",
                DisplayName = "Root Admin",
                PasswordHash = "hash",
                RoleName = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        // Interceptor simulates concurrent transaction mutating target center's row_version right before EF Core executes UPDATE
        var interceptor = new RelationalRaceInterceptor(async cmd =>
        {
            if (cmd.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                && cmd.CommandText.Contains("centers", StringComparison.OrdinalIgnoreCase))
            {
                await using var rawConn = new MySqlConnection(database.ConnectionString);
                await rawConn.OpenAsync();
                await using var rawCmd = rawConn.CreateCommand();
                rawCmd.CommandText = $"UPDATE centers SET row_version = 999 WHERE center_id = '{testCenterId:D}';";
                await rawCmd.ExecuteNonQueryAsync();
                return true;
            }
            return false;
        });

        await using var context = CreateContext(database.ConnectionString, callerContext, interceptor);
        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper = new AuthorizationBootstrapper(context, _mockTimeProvider.Object);
        var service = new PlatformCenterService(
            context,
            callerContext,
            passwordHasher,
            authBootstrapper,
            _mockTimeProvider.Object);

        var updateReq = new UpdatePlatformCenterStatusRequest
        {
            Status = CenterStatus.Suspended.ToString(),
            RowVersion = "1"
        };

        // Service executes: pre-check passes (rowVersion==1), but during SaveChangesAsync the UPDATE matches 0 rows due to race
        // EF Core throws DbUpdateConcurrencyException -> caught by service and mapped to ConcurrencyConflict (409)
        var result = await service.UpdateCenterStatusAsync(testCenterId, updateReq, "trace-race-status");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Contains("thay đổi bởi thao tác khác", result.ErrorMessage);
    }

    [MySqlIntegrationFact]
    public async Task ResetCenterManagerPasswordAsync_RelationalOCCRace_WhenDbUpdateConcurrencyExceptionThrown_ReturnsConcurrencyConflict()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();
        var testCenterId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();

        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.AddRange(
                new Center
                {
                    CenterId = platformCenterId,
                    CenterCode = "PLATFORM",
                    CenterName = "Root Tenant",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Center
                {
                    CenterId = testCenterId,
                    CenterCode = "PW_RACE_CTR",
                    CenterName = "Password Race Center",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );
            setupContext.Users.AddRange(
                new User
                {
                    UserId = platformAdminUserId,
                    CenterId = platformCenterId,
                    Username = "root.admin",
                    DisplayName = "Root Admin",
                    PasswordHash = "hash",
                    RoleName = UserRole.PlatformAdmin,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = managerUserId,
                    CenterId = testCenterId,
                    Username = "pw.manager",
                    DisplayName = "Password Manager",
                    PasswordHash = "InitialHash123",
                    RoleName = UserRole.CenterManager,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );
            await setupContext.SaveChangesAsync();
        }

        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        // Interceptor simulates concurrent modification of the manager user row_version right before EF Core executes UPDATE
        var interceptor = new RelationalRaceInterceptor(async cmd =>
        {
            if (cmd.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                && cmd.CommandText.Contains("users", StringComparison.OrdinalIgnoreCase))
            {
                await using var rawConn = new MySqlConnection(database.ConnectionString);
                await rawConn.OpenAsync();
                await using var rawCmd = rawConn.CreateCommand();
                rawCmd.CommandText = $"UPDATE users SET row_version = 999 WHERE user_id = '{managerUserId:D}';";
                await rawCmd.ExecuteNonQueryAsync();
                return true;
            }
            return false;
        });

        await using var context = CreateContext(database.ConnectionString, callerContext, interceptor);
        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper = new AuthorizationBootstrapper(context, _mockTimeProvider.Object);
        var service = new PlatformCenterService(
            context,
            callerContext,
            passwordHasher,
            authBootstrapper,
            _mockTimeProvider.Object);

        var resetReq = new ResetCenterManagerPasswordRequest
        {
            NewPassword = "NewSecretPassword123!",
            ExpectedUserRowVersion = "1"
        };

        // Service executes: pre-check passes (rowVersion==1), but during SaveChangesAsync the UPDATE matches 0 rows due to race
        // EF Core throws DbUpdateConcurrencyException -> caught by service and mapped to ConcurrencyConflict (409)
        var result = await service.ResetCenterManagerPasswordAsync(testCenterId, managerUserId, resetReq, "trace-race-pw");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Contains("thay đổi bởi thao tác khác", result.ErrorMessage);
    }

    [MySqlIntegrationFact]
    public async Task CreateCenterAsync_RelationalDuplicateKeyRace_WhenMySql1062Thrown_ReturnsDuplicateResource()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();

        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.Add(new Center
            {
                CenterId = platformCenterId,
                CenterCode = "PLATFORM",
                CenterName = "Root Tenant",
                Status = CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            setupContext.Users.Add(new User
            {
                UserId = platformAdminUserId,
                CenterId = platformCenterId,
                Username = "root.admin",
                DisplayName = "Root Admin",
                PasswordHash = "hash",
                RoleName = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        const string raceCenterCode = "RACE_DUP_1062";

        // Interceptor simulates concurrent transaction committing the center code between AnyAsync pre-check and SaveChangesAsync
        var interceptor = new RelationalRaceInterceptor(async cmd =>
        {
            if (cmd.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                && cmd.CommandText.Contains("centers", StringComparison.OrdinalIgnoreCase))
            {
                await using var rawConn = new MySqlConnection(database.ConnectionString);
                await rawConn.OpenAsync();
                await using var rawCmd = rawConn.CreateCommand();
                rawCmd.CommandText = $@"INSERT INTO centers (center_id, center_code, center_name, status, timezone, created_at, updated_at, row_version)
                                       VALUES ('{Guid.NewGuid():D}', '{raceCenterCode}', 'Concurrently Inserted Center', 'Active', 'Asia/Ho_Chi_Minh', '{FixedUtcNow:yyyy-MM-dd HH:mm:ss}', '{FixedUtcNow:yyyy-MM-dd HH:mm:ss}', 1);";
                await rawCmd.ExecuteNonQueryAsync();
                return true;
            }
            return false;
        });

        await using var context = CreateContext(database.ConnectionString, callerContext, interceptor);
        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper = new AuthorizationBootstrapper(context, _mockTimeProvider.Object);
        var service = new PlatformCenterService(
            context,
            callerContext,
            passwordHasher,
            authBootstrapper,
            _mockTimeProvider.Object);

        var request = new CreatePlatformCenterRequest
        {
            CenterCode = raceCenterCode,
            CenterName = "My Race Attempt Center",
            Timezone = "Asia/Ho_Chi_Minh",
            InitialManagerUsername = "race.manager",
            InitialManagerDisplayName = "Race Manager",
            InitialManagerPassword = "StrongPassword123!"
        };

        // Service executes: AnyAsync pre-check returns false (not yet inserted).
        // Then interceptor inserts duplicate center code on separate connection.
        // SaveChangesAsync triggers MySQL 1062 on unique constraint uq_centers_center_code.
        // Service catches DbUpdateException with InnerException MySqlException 1062 -> returns DuplicateResource (409).
        var result = await service.CreateCenterAsync(request, "trace-race-dup");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Contains("đã tồn tại", result.ErrorMessage);
    }

    [MySqlIntegrationFact]
    public async Task CreateCenterAsync_RelationalDuplicateKeyRace_WhenMySql1062OnOtherIndex_RethrowsException()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();

        await using (var setupContext = CreateContext(database.ConnectionString, new TenantContext()))
        {
            setupContext.Centers.Add(new Center
            {
                CenterId = platformCenterId,
                CenterCode = "PLATFORM",
                CenterName = "Platform Root Center",
                Status = CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            setupContext.Users.Add(new User
            {
                UserId = platformAdminUserId,
                CenterId = platformCenterId,
                Username = "platform.admin",
                DisplayName = "Platform Administrator",
                PasswordHash = "hash",
                RoleName = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        // Interceptor causes MySQL 1062 on unique key ux_users_center_id_username instead of ux_centers_center_code
        var interceptor = new RelationalRaceInterceptor(async cmd =>
        {
            if (cmd.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase))
            {
                // Clear parameters and replace command with duplicate insert on ux_users_center_id_username (platform.admin already exists)
                cmd.Parameters.Clear();
                cmd.CommandText = $@"INSERT INTO users (user_id, center_id, username, display_name, password_hash, role_name, status, auth_version, created_at, updated_at, row_version)
                                     VALUES ('{Guid.NewGuid():D}', '{platformCenterId:D}', 'platform.admin', 'Duplicate User', 'hash', 'PlatformAdmin', 'Active', 1, '{FixedUtcNow:yyyy-MM-dd HH:mm:ss}', '{FixedUtcNow:yyyy-MM-dd HH:mm:ss}', 1);";
                return true;
            }
            return false;
        });

        await using var context = CreateContext(database.ConnectionString, callerContext, interceptor);
        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper = new AuthorizationBootstrapper(context, _mockTimeProvider.Object);
        var service = new PlatformCenterService(
            context,
            callerContext,
            passwordHasher,
            authBootstrapper,
            _mockTimeProvider.Object);

        var request = new CreatePlatformCenterRequest
        {
            CenterCode = "UNIQUE_CODE_NOT_COLLIDING",
            CenterName = "Non Colliding Code Center",
            Timezone = "Asia/Ho_Chi_Minh",
            InitialManagerUsername = "unique.manager",
            InitialManagerDisplayName = "Unique Manager",
            InitialManagerPassword = "StrongPassword123!"
        };

        // When 1062 occurs on a key other than ux_centers_center_code (such as ux_users_center_id_username),
        // CreateCenterAsync MUST rethrow the DbUpdateException and NOT swallow/map it as duplicate CenterCode!
        var ex = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await service.CreateCenterAsync(request, "trace-race-other-1062");
        });

        var mysqlEx = ex.GetBaseException() as MySqlException;
        Assert.NotNull(mysqlEx);
        Assert.Equal(1062, mysqlEx.Number);
        Assert.Contains("ux_users_center_id_username", mysqlEx.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ux_centers_center_code", mysqlEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [MySqlIntegrationFact]
    public async Task ResetCenterManagerPassword_ConsecutiveResets_UsingReturnedRowVersions_SucceedsSequentially_And_StaleVersion_ReturnsConflict()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();
        var targetCenterId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();

        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.AddRange(
                new Center
                {
                    CenterId = platformCenterId,
                    CenterCode = "PLATFORM",
                    CenterName = "Root Tenant",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Center
                {
                    CenterId = targetCenterId,
                    CenterCode = "RESET_SEQ_CTR",
                    CenterName = "Sequential Reset Center",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );
            setupContext.Users.AddRange(
                new User
                {
                    UserId = platformAdminUserId,
                    CenterId = platformCenterId,
                    Username = "root.admin",
                    DisplayName = "Root Admin",
                    PasswordHash = "hash",
                    RoleName = UserRole.PlatformAdmin,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = managerUserId,
                    CenterId = targetCenterId,
                    Username = "seq.manager",
                    DisplayName = "Sequential Manager",
                    PasswordHash = "InitialPasswordHash123",
                    RoleName = UserRole.CenterManager,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );
            await setupContext.SaveChangesAsync();
        }

        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        await using var serviceContext = CreateContext(database.ConnectionString, callerContext);
        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper = new AuthorizationBootstrapper(serviceContext, _mockTimeProvider.Object);
        var service = new PlatformCenterService(
            serviceContext,
            callerContext,
            passwordHasher,
            authBootstrapper,
            _mockTimeProvider.Object);

        // 1. Verify initial list returns InitialManagerUserRowVersion = "1"
        var initialList = await service.ListCentersAsync(1, 20, null, null, "trace-list-1");
        Assert.True(initialList.IsSuccess);
        var centerItem1 = initialList.Data!.Items.Single(c => c.CenterId == targetCenterId);
        Assert.Equal("1", centerItem1.InitialManagerUserRowVersion);

        // 2. First password reset with ExpectedUserRowVersion = "1"
        var reset1 = await service.ResetCenterManagerPasswordAsync(
            targetCenterId,
            managerUserId,
            new ResetCenterManagerPasswordRequest
            {
                NewPassword = "FirstNewPassword123!",
                ExpectedUserRowVersion = "1"
            },
            "trace-reset-1");
        Assert.True(reset1.IsSuccess, reset1.ErrorMessage);
        Assert.Equal("2", reset1.Data!.NewUserRowVersion);

        // 3. Verify list now reflects bumped InitialManagerUserRowVersion = "2"
        var secondList = await service.ListCentersAsync(1, 20, null, null, "trace-list-2");
        Assert.True(secondList.IsSuccess);
        var centerItem2 = secondList.Data!.Items.Single(c => c.CenterId == targetCenterId);
        Assert.Equal("2", centerItem2.InitialManagerUserRowVersion);

        // 4. Second password reset using returned row version "2"
        var reset2 = await service.ResetCenterManagerPasswordAsync(
            targetCenterId,
            managerUserId,
            new ResetCenterManagerPasswordRequest
            {
                NewPassword = "SecondNewPassword456!",
                ExpectedUserRowVersion = reset1.Data!.NewUserRowVersion // "2"
            },
            "trace-reset-2");
        Assert.True(reset2.IsSuccess, reset2.ErrorMessage);
        Assert.Equal("3", reset2.Data!.NewUserRowVersion);

        // 5. Stale reset attempt using old version "1" MUST fail with 409 ConcurrencyConflict
        var staleReset1 = await service.ResetCenterManagerPasswordAsync(
            targetCenterId,
            managerUserId,
            new ResetCenterManagerPasswordRequest
            {
                NewPassword = "StalePasswordAttempt789!",
                ExpectedUserRowVersion = "1"
            },
            "trace-stale-1");
        Assert.False(staleReset1.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, staleReset1.ErrorCode);

        // 6. Stale reset attempt using previous version "2" MUST also fail with 409 ConcurrencyConflict
        var staleReset2 = await service.ResetCenterManagerPasswordAsync(
            targetCenterId,
            managerUserId,
            new ResetCenterManagerPasswordRequest
            {
                NewPassword = "StalePasswordAttempt789!",
                ExpectedUserRowVersion = "2"
            },
            "trace-stale-2");
        Assert.False(staleReset2.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, staleReset2.ErrorCode);

        // 7. Verify live MySQL state: AuthVersion bumped to 3, RowVersion bumped to 3, password verifies against latest
        await using var verifyContext = CreateContext(database.ConnectionString, setupTenant);
        var managerUser = await verifyContext.Users.IgnoreQueryFilters().SingleAsync(u => u.UserId == managerUserId);
        Assert.Equal(3u, managerUser.RowVersion);
        Assert.Equal(3u, managerUser.AuthVersion);
        var verifyResult = passwordHasher.VerifyHashedPassword(managerUser, managerUser.PasswordHash, "SecondNewPassword456!");
        Assert.Equal(PasswordVerificationResult.Success, verifyResult);
    }

    [MySqlIntegrationFact]
    public async Task ListCentersAsync_SearchFilter_MatchesManagerUsernameAndDisplayName()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();
        var centerAId = Guid.NewGuid();
        var centerBId = Guid.NewGuid();

        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.AddRange(
                new Center
                {
                    CenterId = platformCenterId,
                    CenterCode = "PLATFORM",
                    CenterName = "Root Tenant",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Center
                {
                    CenterId = centerAId,
                    CenterCode = "CENTER_ALPHA",
                    CenterName = "Alpha Academy",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Center
                {
                    CenterId = centerBId,
                    CenterCode = "CENTER_BETA",
                    CenterName = "Beta Institute",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );
            setupContext.Users.AddRange(
                new User
                {
                    UserId = platformAdminUserId,
                    CenterId = platformCenterId,
                    Username = "root.admin",
                    DisplayName = "Root Admin",
                    PasswordHash = "hash",
                    RoleName = UserRole.PlatformAdmin,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = centerAId,
                    Username = "unique_manager_alpha",
                    DisplayName = "Nguyen Van Manager Alpha",
                    PasswordHash = "hash",
                    RoleName = UserRole.CenterManager,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = centerBId,
                    Username = "unique_manager_beta",
                    DisplayName = "Tran Thi Manager Beta",
                    PasswordHash = "hash",
                    RoleName = UserRole.CenterManager,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );
            await setupContext.SaveChangesAsync();
        }

        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        await using var context = CreateContext(database.ConnectionString, callerContext);
        var service = new PlatformCenterService(
            context,
            callerContext,
            new PasswordHasher<User>(),
            new AuthorizationBootstrapper(context, _mockTimeProvider.Object),
            _mockTimeProvider.Object);

        // Search by manager username
        var searchUserRes = await service.ListCentersAsync(1, 20, "unique_manager_alpha", null, "trace-s1");
        Assert.True(searchUserRes.IsSuccess);
        Assert.Single(searchUserRes.Data!.Items);
        Assert.Equal("CENTER_ALPHA", searchUserRes.Data.Items[0].CenterCode);

        // Search by manager display name
        var searchDisplayRes = await service.ListCentersAsync(1, 20, "Tran Thi Manager Beta", null, "trace-s2");
        Assert.True(searchDisplayRes.IsSuccess);
        Assert.Single(searchDisplayRes.Data!.Items);
        Assert.Equal("CENTER_BETA", searchDisplayRes.Data.Items[0].CenterCode);

        // Search non-existent
        var searchNoneRes = await service.ListCentersAsync(1, 20, "NonExistentPerson", null, "trace-s3");
        Assert.True(searchNoneRes.IsSuccess);
        Assert.Empty(searchNoneRes.Data!.Items);
    }

    [MySqlIntegrationFact]
    public async Task UpdateCenterStatusAsync_Suspended_EvictsSessionsAndRevokesRefreshTokensInMySql()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var platformAdminUserId = Guid.NewGuid();
        var targetCenterId = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var setupTenant = new TenantContext();
        await using (var setupContext = CreateContext(database.ConnectionString, setupTenant))
        {
            setupContext.Centers.AddRange(
                new Center
                {
                    CenterId = platformCenterId,
                    CenterCode = "PLATFORM",
                    CenterName = "Root Tenant",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Center
                {
                    CenterId = targetCenterId,
                    CenterCode = "TARGET_CTR",
                    CenterName = "Target Center",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );

            setupContext.Users.AddRange(
                new User
                {
                    UserId = platformAdminUserId,
                    CenterId = platformCenterId,
                    Username = "root.admin",
                    DisplayName = "Root Admin",
                    PasswordHash = "hash",
                    RoleName = UserRole.PlatformAdmin,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = user1Id,
                    CenterId = targetCenterId,
                    Username = "user.one",
                    DisplayName = "User One",
                    PasswordHash = "hash",
                    RoleName = UserRole.Teacher,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = user2Id,
                    CenterId = targetCenterId,
                    Username = "user.two",
                    DisplayName = "User Two",
                    PasswordHash = "hash",
                    RoleName = UserRole.Student,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );

            setupContext.RefreshTokens.AddRange(
                new RefreshToken
                {
                    CenterId = targetCenterId,
                    UserId = user1Id,
                    TokenHash = "token_hash_1",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    CreatedAt = FixedUtcNow
                },
                new RefreshToken
                {
                    CenterId = targetCenterId,
                    UserId = user1Id,
                    TokenHash = "token_hash_2",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    CreatedAt = FixedUtcNow
                },
                new RefreshToken
                {
                    CenterId = targetCenterId,
                    UserId = user2Id,
                    TokenHash = "token_hash_3",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    CreatedAt = FixedUtcNow
                }
            );

            await setupContext.SaveChangesAsync();
        }

        var callerContext = new TenantContext();
        callerContext.Initialize(platformCenterId, platformAdminUserId, nameof(UserRole.PlatformAdmin), 1);

        await using var serviceContext = CreateContext(database.ConnectionString, callerContext);
        var passwordHasher = new PasswordHasher<User>();
        var authBootstrapper = new AuthorizationBootstrapper(serviceContext, _mockTimeProvider.Object);
        var service = new PlatformCenterService(
            serviceContext,
            callerContext,
            passwordHasher,
            authBootstrapper,
            _mockTimeProvider.Object);

        var request = new UpdatePlatformCenterStatusRequest
        {
            Status = CenterStatus.Suspended.ToString(),
            RowVersion = "1"
        };

        var result = await service.UpdateCenterStatusAsync(targetCenterId, request, "trace-mysql-suspend");
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(CenterStatus.Suspended.ToString(), result.Data!.Status);

        // Verify in live MySQL:
        // 1. Both users have auth_version bumped to 2
        // 2. All 3 refresh tokens are revoked with reason containing "suspended"
        await using var verifyContext = CreateContext(database.ConnectionString, setupTenant);
        var users = await verifyContext.Users.IgnoreQueryFilters().Where(u => u.CenterId == targetCenterId).ToListAsync();
        Assert.Equal(2, users.Count);
        Assert.All(users, u => Assert.Equal(2u, u.AuthVersion));

        var tokens = await verifyContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == user1Id || t.UserId == user2Id)
            .ToListAsync();
        Assert.Equal(3, tokens.Count);
        Assert.All(tokens, t =>
        {
            Assert.NotNull(t.RevokedAt);
            Assert.Contains("suspended", t.RevokeReason, StringComparison.OrdinalIgnoreCase);
        });
    }

    [MySqlIntegrationFact]
    public async Task Migration_Down_RollsBackPlatformAdmin_WithoutFkViolation()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var tenant = new TenantContext();
        await using var context = CreateContext(database.ConnectionString, tenant);

        // Provision Platform Admin using PlatformAdminProvisioner
        var options = Microsoft.Extensions.Options.Options.Create(new PlatformBootstrapOptions
        {
            AdminUsername = "platform.admin",
            AdminDisplayName = "Platform Administrator",
            AdminPassword = "BootstrapPassword123!"
        });

        var authBootstrapper = new AuthorizationBootstrapper(context, _mockTimeProvider.Object);
        var provisioner = new PlatformAdminProvisioner(
            context,
            new PasswordHasher<User>(),
            authBootstrapper,
            options,
            _mockTimeProvider.Object,
            Mock.Of<ILogger<PlatformAdminProvisioner>>());

        await provisioner.EnsureAsync();

        // Verify provisioned state
        Assert.True(await context.Users.IgnoreQueryFilters().AnyAsync(u => u.RoleName == UserRole.PlatformAdmin));
        Assert.True(await context.UserRoleAssignments.IgnoreQueryFilters().AnyAsync(ur => ur.AccountType == UserRole.PlatformAdmin));
        Assert.True(await context.PermissionAccountTypes.IgnoreQueryFilters().AnyAsync(pat => pat.AccountType == UserRole.PlatformAdmin));

        // Execute Down() migration to previous Gate 1 freeze migration: 20260911092808_HardenRecommendationGenerationState
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260911092808_HardenRecommendationGenerationState");

        // Verify Down() succeeded cleanly without foreign key violations!
        // Now re-apply migration forward to latest
        await migrator.MigrateAsync();

        // Verify database is back at Gate 2 schema and ready
        var count = await context.Permissions.IgnoreQueryFilters().CountAsync();
        Assert.Equal(66, count);
    }

    [MySqlIntegrationFact]
    public async Task BootstrapPlatformAsync_FailClosed_RejectsMalformedPlatformTenantState()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var tenant = new TenantContext();
        await using var context = CreateContext(database.ConnectionString, tenant);

        // Seed corrupted root center: wrong CenterCode
        context.Centers.Add(new Center
        {
            CenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            CenterCode = "CORRUPT_CODE", // MUST be "PLATFORM"
            CenterName = "Corrupt Platform",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });
        await context.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new PlatformBootstrapOptions
        {
            AdminUsername = "platform.admin",
            AdminDisplayName = "Platform Administrator",
            AdminPassword = "BootstrapPassword123!"
        });

        var authBootstrapper = new AuthorizationBootstrapper(context, _mockTimeProvider.Object);
        var provisioner = new PlatformAdminProvisioner(
            context,
            new PasswordHasher<User>(),
            authBootstrapper,
            options,
            _mockTimeProvider.Object,
            Mock.Of<ILogger<PlatformAdminProvisioner>>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await provisioner.EnsureAsync();
        });

        Assert.Contains("malformed state", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static EduTwinDbContext CreateContext(
        string connectionString,
        ITenantIdAccessor tenant,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        if (interceptors != null && interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }
        return new EduTwinDbContext(options.Options, tenant);
    }

    private sealed class RelationalRaceInterceptor : DbCommandInterceptor
    {
        private readonly Func<DbCommand, Task<bool>> _actionAsync;
        private int _executed;

        public RelationalRaceInterceptor(Func<DbCommand, Task<bool>> actionAsync)
        {
            _actionAsync = actionAsync;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _executed) == 0)
            {
                var matched = await _actionAsync(command);
                if (matched)
                {
                    Interlocked.Exchange(ref _executed, 1);
                }
            }
            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _executed) == 0)
            {
                var matched = await _actionAsync(command);
                if (matched)
                {
                    Interlocked.Exchange(ref _executed, 1);
                }
            }
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<MySqlTestDatabase> CreateAsync()
        {
            var configuredConnection = Environment.GetEnvironmentVariable(AdminConnectionVariable)
                ?? throw new InvalidOperationException($"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_plat_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            var database = new MySqlTestDatabase(
                adminBuilder.ConnectionString,
                databaseName,
                databaseBuilder.ConnectionString);
            try
            {
                var tenant = new TenantContext();
                await using var context = CreateContext(database.ConnectionString, tenant);
                await context.Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_adminConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
