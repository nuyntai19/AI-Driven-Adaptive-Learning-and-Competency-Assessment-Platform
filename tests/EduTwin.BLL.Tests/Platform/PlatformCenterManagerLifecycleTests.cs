using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

namespace EduTwin.BLL.Tests.Platform;

public class PlatformCenterManagerLifecycleTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IPasswordHasher<User>> _mockPasswordHasher;
    private readonly AuthorizationBootstrapper _authorizationBootstrapper;
    private readonly PlatformCenterService _sut;
    private readonly Guid _platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
    private readonly Guid _platformAdminUserId = Guid.NewGuid();
    private static readonly DateTime FixedUtcNow = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);
    private readonly Mock<TimeProvider> _mockTimeProvider;

    public PlatformCenterManagerLifecycleTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        _mockPasswordHasher
            .Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed_secure_password_123");

        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider
            .Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
        _authorizationBootstrapper = new AuthorizationBootstrapper(_dbContext, _mockTimeProvider.Object);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_platformCenterId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_platformAdminUserId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.PlatformAdmin));

        _sut = new PlatformCenterService(
            _dbContext,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            _authorizationBootstrapper,
            _mockTimeProvider.Object);

        // Seed Root Tenant PLATFORM
        _dbContext.Centers.Add(new Center
        {
            CenterId = _platformCenterId,
            CenterCode = "PLATFORM",
            CenterName = "EduTwin Platform Root",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            PrimaryManagerUserId = null,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private (Center Center, User Manager) SeedCustomerCenterWithManager(
        string centerCode = "CENTER_01",
        string managerUsername = "mgr1")
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var center = new Center
        {
            CenterId = centerId,
            CenterCode = centerCode,
            CenterName = $"Test Center {centerCode}",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            PrimaryManagerUserId = managerId,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        };

        var manager = new User
        {
            UserId = managerId,
            CenterId = centerId,
            Username = managerUsername,
            DisplayName = $"Manager {managerUsername}",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            PasswordHash = "hashed_default_pw",
            AuthVersion = 1,
            CreatedAt = FixedUtcNow,
            CreatedBy = _platformAdminUserId,
            UpdatedAt = FixedUtcNow,
            UpdatedBy = _platformAdminUserId,
            RowVersion = 1
        };

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(manager);
        _dbContext.SaveChanges();

        return (center, manager);
    }

    [Fact]
    public async Task ListCenterManagersAsync_WhenCallerNotPlatformAdmin_ReturnsForbidden()
    {
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));
        var (center, _) = SeedCustomerCenterWithManager();

        var result = await _sut.ListCenterManagersAsync(center.CenterId, 1, 20, null, null, "trace-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task ListCenterManagersAsync_WhenTargetIsPlatformCenter_ReturnsForbidden()
    {
        var result = await _sut.ListCenterManagersAsync(_platformCenterId, 1, 20, null, null, "trace-2");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task ListCenterManagersAsync_Success_ReturnsPrimaryManagerFirst()
    {
        var (center, primaryManager) = SeedCustomerCenterWithManager("CTR_A", "primary_mgr");

        var secondManager = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = center.CenterId,
            Username = "second_mgr",
            DisplayName = "Second Manager",
            PasswordHash = "hashed_pw",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = FixedUtcNow.AddMinutes(-10), // Created earlier
            UpdatedAt = FixedUtcNow.AddMinutes(-10),
            RowVersion = 1
        };
        _dbContext.Users.Add(secondManager);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ListCenterManagersAsync(center.CenterId, 1, 20, null, null, "trace-3");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
        // Primary manager must be first regardless of CreatedAt
        Assert.Equal(primaryManager.UserId, result.Data.Items[0].UserId);
        Assert.True(result.Data.Items[0].IsPrimary);
        Assert.Equal(secondManager.UserId, result.Data.Items[1].UserId);
        Assert.False(result.Data.Items[1].IsPrimary);
    }

    [Fact]
    public async Task CreateCenterManagerAsync_PasswordUnder12Chars_ReturnsValidationFailed()
    {
        var (center, _) = SeedCustomerCenterWithManager("CTR_B", "mgr_b");

        var request = new CreateCenterManagerRequest
        {
            Username = "new_manager",
            DisplayName = "New Manager",
            Password = "short", // < 12 characters
            ExpectedCenterRowVersion = center.RowVersion.ToString(),
            Reason = "Adding secondary manager for operations."
        };

        var result = await _sut.CreateCenterManagerAsync(center.CenterId, request, "trace-4");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCenterManagerAsync_StaleCenterRowVersion_ReturnsConcurrencyConflict()
    {
        var (center, _) = SeedCustomerCenterWithManager("CTR_C", "mgr_c");

        var request = new CreateCenterManagerRequest
        {
            Username = "new_manager_c",
            DisplayName = "New Manager C",
            Password = "Password123456!",
            ExpectedCenterRowVersion = "9999", // Mismatch
            Reason = "Adding manager with stale version."
        };

        var result = await _sut.CreateCenterManagerAsync(center.CenterId, request, "trace-5");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCenterManagerAsync_DuplicateUsername_ReturnsDuplicateResource()
    {
        var (center, primary) = SeedCustomerCenterWithManager("CTR_D", "mgr_d");

        var request = new CreateCenterManagerRequest
        {
            Username = primary.Username, // Duplicate in same center
            DisplayName = "Another Manager",
            Password = "Password123456!",
            ExpectedCenterRowVersion = center.RowVersion.ToString(),
            Reason = "Attempt duplicate username."
        };

        var result = await _sut.CreateCenterManagerAsync(center.CenterId, request, "trace-6");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCenterManagerAsync_Success_BumpsCenterRowVersionAndCreatesRedactedAudit()
    {
        var (center, _) = SeedCustomerCenterWithManager("CTR_E", "mgr_e");
        var originalCenterVersion = center.RowVersion;

        var request = new CreateCenterManagerRequest
        {
            Username = "deputy_mgr_e",
            DisplayName = "Deputy Manager E",
            Password = "SuperSecretPassword123!",
            ExpectedCenterRowVersion = originalCenterVersion.ToString(),
            Reason = "Operational backup manager."
        };

        var result = await _sut.CreateCenterManagerAsync(center.CenterId, request, "trace-7");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("deputy_mgr_e", result.Data.Username);
        Assert.False(result.Data.IsPrimary); // Center already had a primary manager

        // Verify Center RowVersion bumped
        var updatedCenter = await _dbContext.Centers.FindAsync(center.CenterId);
        Assert.NotNull(updatedCenter);
        Assert.Equal(originalCenterVersion + 1, updatedCenter.RowVersion);

        // Verify Redacted Audit Log
        var audit = await _dbContext.AuthorizationAuditLogs
            .FirstOrDefaultAsync(a => a.ActionType == "CenterManagerCreated" && a.TargetId == $"{center.CenterId:D}:{result.Data.UserId:D}");
        Assert.NotNull(audit);
        Assert.Equal(_platformCenterId, audit.CenterId);
        Assert.Null(audit.TargetUserId); // Cross-tenant target_user_id must be null
        Assert.DoesNotContain("SuperSecretPassword123!", audit.AfterData ?? string.Empty);
    }

    [Fact]
    public async Task UpdateCenterManagerStatusAsync_CannotLockOrDisablePrimaryManagerWithoutTransfer()
    {
        var (center, primary) = SeedCustomerCenterWithManager("CTR_F", "primary_f");

        var request = new UpdateCenterManagerStatusRequest
        {
            Status = nameof(UserStatus.Disabled),
            ExpectedUserRowVersion = primary.RowVersion.ToString(),
            Reason = "Direct disable primary."
        };

        var result = await _sut.UpdateCenterManagerStatusAsync(center.CenterId, primary.UserId, request, "trace-8");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterManagerStatusAsync_CannotDisableLastActiveManagerOfActiveCenter()
    {
        var (center, primary) = SeedCustomerCenterWithManager("CTR_G", "primary_g");

        // Even if not primary (let's say another manager is primary), if only 1 active manager remains:
        var secondManager = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = center.CenterId,
            Username = "second_g",
            DisplayName = "Second G",
            PasswordHash = "hashed_pw",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Disabled, // already disabled
            AuthVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        };
        _dbContext.Users.Add(secondManager);
        await _dbContext.SaveChangesAsync();

        // Primary is the ONLY active manager
        var request = new UpdateCenterManagerStatusRequest
        {
            Status = nameof(UserStatus.Locked),
            ExpectedUserRowVersion = primary.RowVersion.ToString(),
            Reason = "Lock last active manager."
        };

        var result = await _sut.UpdateCenterManagerStatusAsync(center.CenterId, primary.UserId, request, "trace-9");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterManagerStatusAsync_DisablingManager_BumpsAuthVersionAndRevokesRefreshTokens()
    {
        var (center, _) = SeedCustomerCenterWithManager("CTR_H", "primary_h");

        var deputy = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = center.CenterId,
            Username = "deputy_h",
            DisplayName = "Deputy H",
            PasswordHash = "hashed_pw",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        };
        _dbContext.Users.Add(deputy);

        var token = new RefreshToken
        {
            CenterId = center.CenterId,
            UserId = deputy.UserId,
            TokenHash = "hash123",
            ExpiresAt = FixedUtcNow.AddDays(7),
            CreatedAt = FixedUtcNow
        };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateCenterManagerStatusRequest
        {
            Status = nameof(UserStatus.Disabled),
            ExpectedUserRowVersion = deputy.RowVersion.ToString(),
            Reason = "Deputy left company."
        };

        var result = await _sut.UpdateCenterManagerStatusAsync(center.CenterId, deputy.UserId, request, "trace-10");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(nameof(UserStatus.Disabled), result.Data.Status);

        var updatedDeputy = await _dbContext.Users.FindAsync(deputy.UserId);
        Assert.NotNull(updatedDeputy);
        Assert.Equal(2u, updatedDeputy.AuthVersion); // AuthVersion bumped

        var updatedToken = await _dbContext.RefreshTokens.FindAsync(token.RefreshTokenId);
        Assert.NotNull(updatedToken);
        Assert.NotNull(updatedToken.RevokedAt); // Refresh token revoked
    }

    [Fact]
    public async Task MakePrimaryCenterManagerAsync_TargetNotActive_ReturnsValidationFailed()
    {
        var (center, _) = SeedCustomerCenterWithManager("CTR_I", "primary_i");

        var lockedDeputy = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = center.CenterId,
            Username = "deputy_i",
            DisplayName = "Deputy I",
            PasswordHash = "hashed_pw",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Locked, // NOT ACTIVE
            AuthVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        };
        _dbContext.Users.Add(lockedDeputy);
        await _dbContext.SaveChangesAsync();

        var request = new MakePrimaryCenterManagerRequest
        {
            ExpectedCenterRowVersion = center.RowVersion.ToString(),
            ExpectedManagerUserRowVersion = lockedDeputy.RowVersion.ToString(),
            DisablePreviousPrimary = false,
            Reason = "Attempt transfer to locked manager."
        };

        var result = await _sut.MakePrimaryCenterManagerAsync(center.CenterId, lockedDeputy.UserId, request, "trace-11");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task MakePrimaryCenterManagerAsync_Success_TransfersAndOptionallyDisablesPreviousPrimary()
    {
        var (center, oldPrimary) = SeedCustomerCenterWithManager("CTR_J", "old_primary_j");

        var newPrimary = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = center.CenterId,
            Username = "new_primary_j",
            DisplayName = "New Primary J",
            PasswordHash = "hashed_pw",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        };
        _dbContext.Users.Add(newPrimary);

        var oldPrimaryToken = new RefreshToken
        {
            CenterId = center.CenterId,
            UserId = oldPrimary.UserId,
            TokenHash = "old_token_hash",
            ExpiresAt = FixedUtcNow.AddDays(7),
            CreatedAt = FixedUtcNow
        };
        _dbContext.RefreshTokens.Add(oldPrimaryToken);
        await _dbContext.SaveChangesAsync();

        var request = new MakePrimaryCenterManagerRequest
        {
            ExpectedCenterRowVersion = center.RowVersion.ToString(),
            ExpectedManagerUserRowVersion = newPrimary.RowVersion.ToString(),
            DisablePreviousPrimary = true,
            ExpectedPreviousPrimaryUserRowVersion = oldPrimary.RowVersion.ToString(),
            Reason = "Transfer primary and disable retiring manager."
        };

        var result = await _sut.MakePrimaryCenterManagerAsync(center.CenterId, newPrimary.UserId, request, "trace-12");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(newPrimary.UserId, result.Data.PrimaryManagerUserId);
        Assert.True(result.Data.PreviousPrimaryDisabled);

        // Verify Center state
        var updatedCenter = await _dbContext.Centers.FindAsync(center.CenterId);
        Assert.NotNull(updatedCenter);
        Assert.Equal(newPrimary.UserId, updatedCenter.PrimaryManagerUserId);

        // Verify Old Primary state
        var updatedOldPrimary = await _dbContext.Users.FindAsync(oldPrimary.UserId);
        Assert.NotNull(updatedOldPrimary);
        Assert.Equal(UserStatus.Disabled, updatedOldPrimary.Status);
        Assert.Equal(2u, updatedOldPrimary.AuthVersion);

        var updatedOldToken = await _dbContext.RefreshTokens.FindAsync(oldPrimaryToken.RefreshTokenId);
        Assert.NotNull(updatedOldToken);
        Assert.NotNull(updatedOldToken.RevokedAt);
    }

    [Fact]
    public async Task UpdateCenterStatusAsync_ReactivateWithoutActivePrimaryManager_ReturnsValidationFailed()
    {
        var (center, primary) = SeedCustomerCenterWithManager("CTR_K", "mgr_k");
        center.Status = CenterStatus.Suspended;
        primary.Status = UserStatus.Disabled; // Primary is disabled
        await _dbContext.SaveChangesAsync();

        var request = new UpdatePlatformCenterStatusRequest
        {
            Status = nameof(CenterStatus.Active),
            RowVersion = center.RowVersion.ToString(),
            Reason = "Reactivate center."
        };

        var result = await _sut.UpdateCenterStatusAsync(center.CenterId, request, "trace-13");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }
}
