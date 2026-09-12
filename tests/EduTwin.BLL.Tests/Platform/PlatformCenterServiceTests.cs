using System;
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

public class PlatformCenterServiceTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IPasswordHasher<User>> _mockPasswordHasher;
    private readonly AuthorizationBootstrapper _authorizationBootstrapper;
    private readonly PlatformCenterService _sut;
    private readonly Guid _platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
    private readonly Guid _platformAdminUserId = Guid.NewGuid();

    public PlatformCenterServiceTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        _mockPasswordHasher
            .Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed_password_123");

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
        _authorizationBootstrapper = new AuthorizationBootstrapper(_dbContext, TimeProvider.System);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_platformCenterId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_platformAdminUserId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.PlatformAdmin));

        _sut = new PlatformCenterService(
            _dbContext,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            _authorizationBootstrapper,
            TimeProvider.System);

        // Seed Root Tenant PLATFORM
        _dbContext.Centers.Add(new Center
        {
            CenterId = _platformCenterId,
            CenterCode = "PLATFORM",
            CenterName = "EduTwin Platform Administration",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ListCentersAsync_WhenCallerIsNotPlatformAdmin_ReturnsForbiddenResource()
    {
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ListCentersAsync(1, 20, null, null, "trace-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task ListCentersAsync_WhenZeroCustomerCentersExist_ReturnsEmptyListSuccessfully()
    {
        var result = await _sut.ListCentersAsync(1, 20, null, null, "trace-2");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Fact]
    public async Task CreateCenterAsync_WhenCenterCodeIsPlatform_ReturnsForbiddenResource()
    {
        var request = new CreatePlatformCenterRequest
        {
            CenterCode = "PLATFORM",
            CenterName = "Duplicate Platform",
            Timezone = "Asia/Bangkok",
            InitialManagerUsername = "manager1",
            InitialManagerDisplayName = "Manager 1",
            InitialManagerPassword = "Password123!"
        };

        var result = await _sut.CreateCenterAsync(request, "trace-3");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task CreateCenterAsync_ValidRequest_CreatesCenterAndRecordsPlatformAuditWithNullTargetUserId()
    {
        var request = new CreatePlatformCenterRequest
        {
            CenterCode = "CENTER_NEW",
            CenterName = "New Experimental Center",
            Timezone = "Asia/Bangkok",
            InitialManagerUsername = "manager_new",
            InitialManagerDisplayName = "Nguyễn Quản Trị",
            InitialManagerPassword = "SecurePassword123!"
        };

        var result = await _sut.CreateCenterAsync(request, "trace-4");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("CENTER_NEW", result.Data.CenterCode);
        Assert.Equal("Active", result.Data.Status);

        // Verify Platform Audit Invariant
        var audit = await _dbContext.AuthorizationAuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.ActionType == "CenterCreated");

        Assert.NotNull(audit);
        Assert.Equal(_platformCenterId, audit.CenterId);
        Assert.Equal(_platformAdminUserId, audit.ActorUserId);
        Assert.Null(audit.TargetUserId); // CRITICAL PLATFORM AUDIT INVARIANT
        Assert.Equal("Center", audit.TargetType);
        Assert.Equal(result.Data.CenterId.ToString("D"), audit.TargetId);
        Assert.DoesNotContain("SecurePassword123!", audit.AfterData ?? string.Empty);
    }

    [Fact]
    public async Task UpdateCenterStatusAsync_WhenTargetIsRootTenantPlatform_ReturnsForbiddenResource()
    {
        var request = new UpdatePlatformCenterStatusRequest
        {
            Status = "Suspended",
            RowVersion = "1"
        };

        var result = await _sut.UpdateCenterStatusAsync(_platformCenterId, request, "trace-5");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterStatusAsync_WhenRowVersionMismatch_ReturnsConcurrencyConflict()
    {
        var centerId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "CENTER_B",
            CenterName = "Center B",
            Status = CenterStatus.Active,
            Timezone = "UTC",
            RowVersion = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = new UpdatePlatformCenterStatusRequest
        {
            Status = "Suspended",
            RowVersion = "999" // Mismatch (center.RowVersion is 1)
        };

        var result = await _sut.UpdateCenterStatusAsync(centerId, request, "trace-6");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task ResetCenterManagerPasswordAsync_ValidRequest_BumpsAuthVersionRevokesTokensAndPreservesAuditInvariant()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "CENTER_C",
            CenterName = "Center C",
            Status = CenterStatus.Active,
            Timezone = "UTC",
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var manager = new User
        {
            UserId = managerId,
            CenterId = centerId,
            Username = "manager_c",
            DisplayName = "Manager C",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            AuthVersion = 1,
            RowVersion = 1,
            PasswordHash = "old_hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(manager);

        var activeToken = new RefreshToken
        {
            RefreshTokenId = 100,
            CenterId = centerId,
            UserId = managerId,
            TokenHash = "token_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.Add(activeToken);
        await _dbContext.SaveChangesAsync();

        var request = new ResetCenterManagerPasswordRequest
        {
            NewPassword = "BrandNewSuperSecret123!",
            ExpectedUserRowVersion = "1"
        };

        var result = await _sut.ResetCenterManagerPasswordAsync(centerId, managerId, request, "trace-7");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Success);
        Assert.Equal("2", result.Data.NewUserRowVersion);

        // Verify user state: password updated, auth_version bumped
        var updatedManager = await _dbContext.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == managerId);
        Assert.Equal(2u, updatedManager.AuthVersion);
        Assert.Equal(2ul, updatedManager.RowVersion);

        // Verify refresh token revoked
        var updatedToken = await _dbContext.RefreshTokens.IgnoreQueryFilters().FirstAsync(rt => rt.RefreshTokenId == 100);
        Assert.NotNull(updatedToken.RevokedAt);

        // Verify audit log
        var audit = await _dbContext.AuthorizationAuditLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.ActionType == "CenterManagerPasswordReset");

        Assert.NotNull(audit);
        Assert.Equal(_platformCenterId, audit.CenterId);
        Assert.Null(audit.TargetUserId); // Cross-tenant target isolation
        Assert.Equal($"{centerId:D}:{managerId:D}", audit.TargetId);
        Assert.DoesNotContain("BrandNewSuperSecret123!", audit.AfterData ?? string.Empty);
    }
}
