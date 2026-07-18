using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class RefreshUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<IRefreshTokenCodec> _mockCodec;
    private readonly Mock<IRefreshTokenStore> _mockStore;
    private readonly Mock<IBackgroundTenantScopeFactory> _mockScopeFactory;
    private readonly Mock<IJwtTokenGenerator> _mockJwtGen;
    private readonly FixedTimeProvider _timeProvider;
    private readonly RefreshUseCase _sut;

    public RefreshUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockCodec = new Mock<IRefreshTokenCodec>();
        _mockStore = new Mock<IRefreshTokenStore>();
        _mockScopeFactory = new Mock<IBackgroundTenantScopeFactory>();
        _mockJwtGen = new Mock<IJwtTokenGenerator>();
        _timeProvider = new FixedTimeProvider(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _mockScopeFactory.Setup(x => x.BeginScope(It.IsAny<Guid>()))
            .Returns<Guid>(centerId =>
            {
                mockAccessor.Setup(a => a.CenterId).Returns(centerId);
                var mockDisposable = new Mock<IDisposable>();
                mockDisposable.Setup(d => d.Dispose()).Callback(() => mockAccessor.Setup(a => a.CenterId).Returns(Guid.Empty));
                return mockDisposable.Object;
            });

        _mockCodec.Setup(c => c.IsValidRawToken(It.IsAny<string>())).Returns(true);

        _sut = new RefreshUseCase(
            _mockCodec.Object,
            _mockStore.Object,
            _mockScopeFactory.Object,
            _mockJwtGen.Object,
            _timeProvider,
            _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static Center MakeCenter(Guid centerId, string code = "C1", CenterStatus status = CenterStatus.Active) =>
        new() { CenterId = centerId, CenterCode = code, CenterName = $"Center {code}", Status = status, Timezone = "UTC", CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

    private static User MakeUser(Guid userId, Guid centerId, Center center, UserStatus status = UserStatus.Active) =>
        new() { UserId = userId, CenterId = centerId, Username = "test", PasswordHash = "hash", RoleName = UserRole.Student, DisplayName = "Test", Status = status, Center = center, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public async Task Refresh_ValidToken_RotatesSuccessfully()
    {
        var rawToken = "raw-old-token";
        var tokenHash = "hash-old-token";
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockCodec.Setup(c => c.HashToken(rawToken)).Returns(tokenHash);
        _mockStore.Setup(s => s.ResolveCenterIdAsync(tokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var center = MakeCenter(centerId);
        var user = MakeUser(userId, centerId, center);
        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var oldToken = new RefreshToken
        {
            RefreshTokenId = 1,
            TokenHash = tokenHash,
            CenterId = centerId,
            UserId = userId,
            User = user,
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1)
        };

        _mockStore.Setup(s => s.GetTokenAsync(tokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(oldToken);
        _mockJwtGen.Setup(g => g.GenerateToken(user, centerId)).Returns("new-jwt");
        _mockCodec.Setup(c => c.GenerateRawToken()).Returns("raw-new-token");
        _mockCodec.Setup(c => c.HashToken("raw-new-token")).Returns("hash-new-token");

        _mockStore.Setup(s => s.RotateTokenAsync(1, "Rotated", "1.1.1.1", It.IsAny<DateTime>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(rawToken, "1.1.1.1");

        Assert.True(result.IsSuccess);
        Assert.Equal("new-jwt", result.Data!.AccessToken);
        Assert.Equal("raw-new-token", result.RawRefreshToken);

        // Assert New Expiry is 30 days
        var expectedExpiry = _timeProvider.GetUtcNow().UtcDateTime.AddDays(30);
        Assert.Equal(expectedExpiry, result.RefreshTokenExpiresAt);

        _mockStore.Verify(s => s.RotateTokenAsync(
            1, "Rotated", "1.1.1.1", _timeProvider.GetUtcNow().UtcDateTime,
            It.Is<RefreshToken>(t => t.TokenHash == "hash-new-token" && t.ExpiresAt == expectedExpiry),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_MalformedToken_ReturnsInvalid_WithoutLookup()
    {
        _mockCodec.Setup(c => c.IsValidRawToken(It.IsAny<string>())).Returns(false);
        var result = await _sut.ExecuteAsync("invalid", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);

        _mockStore.Verify(s => s.ResolveCenterIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_NullToken_ReturnsInvalid_WithoutHashOrLookup()
    {
        var result = await _sut.ExecuteAsync(null, "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);

        _mockStore.Verify(s => s.ResolveCenterIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCodec.Verify(c => c.HashToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_UnknownToken_ReturnsInvalid()
    {
        _mockCodec.Setup(c => c.HashToken(It.IsAny<string>())).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_ExpiresAt_EqualsNow_ReturnsInvalid()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var expiredToken = new RefreshToken
        {
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(expiredToken);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_ExpiredBeforeNow_ReturnsInvalid()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var expiredToken = new RefreshToken
        {
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1)
        };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(expiredToken);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_RevokedToken_ReturnsInvalid()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var revokedToken = new RefreshToken
        {
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1),
            RevokedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(revokedToken);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_ReplacedToken_ReturnsInvalid()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var replacedToken = new RefreshToken
        {
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1),
            ReplacedByTokenId = 999
        };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(replacedToken);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_UserDisabled_ReturnsUserDisabled()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var user = new User { Status = UserStatus.Disabled, CenterId = centerId };
        var token = new RefreshToken { ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1), User = user, CenterId = centerId };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_UserSoftDeleted_ReturnsUserDisabled()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var user = new User { IsDeleted = true, CenterId = centerId };
        var token = new RefreshToken { ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1), User = user, CenterId = centerId };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_UserLocked_ReturnsUserDisabled()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var user = new User { Status = UserStatus.Locked, CenterId = centerId };
        var token = new RefreshToken { ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1), User = user, CenterId = centerId };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_CenterSoftDeleted_ReturnsUserDisabled()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var center = MakeCenter(centerId);
        center.IsDeleted = true;
        var user = MakeUser(Guid.NewGuid(), centerId, center);
        _dbContext.Centers.Add(center);
        await _dbContext.SaveChangesAsync();

        var token = new RefreshToken { ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1), User = user, CenterId = centerId };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_CenterSuspended_ReturnsUserDisabled()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var center = MakeCenter(centerId, status: CenterStatus.Suspended);
        var user = MakeUser(Guid.NewGuid(), centerId, center);
        _dbContext.Centers.Add(center);
        await _dbContext.SaveChangesAsync();

        var token = new RefreshToken { ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1), User = user, CenterId = centerId };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task Refresh_ConcurrencyAffected0_RollbacksAndReturnsInvalid()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var center = MakeCenter(centerId);
        var user = MakeUser(userId, centerId, center);
        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var oldToken = new RefreshToken
        {
            RefreshTokenId = 1,
            TokenHash = "hash",
            CenterId = centerId,
            UserId = userId,
            User = user,
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1)
        };

        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(oldToken);
        _mockJwtGen.Setup(g => g.GenerateToken(user, centerId)).Returns("new-jwt");
        _mockCodec.Setup(c => c.GenerateRawToken()).Returns("new-raw");
        _mockCodec.Setup(c => c.HashToken("new-raw")).Returns("new-hash");

        // Affected rows = 0
        _mockStore.Setup(s => s.RotateTokenAsync(1, "Rotated", "1.1.1.1", It.IsAny<DateTime>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);

        // No DB changes should be made by BLL
        // In reality, the Store rolls back the transaction.
        // We explicitly document this is tested in P06-T04-B via MySQL runtime concurrency tests.
    }

    [Fact]
    public async Task Refresh_CrossTenantTokenMismatch_DoesNotRotate()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var token = new RefreshToken { ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1), CenterId = Guid.NewGuid(), User = new User { CenterId = centerId } };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await _sut.ExecuteAsync("raw", "1.1.1.1");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result.ErrorCode);
        _mockStore.Verify(s => s.RotateTokenAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_NullClientIp_PassedAsNull()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var center = MakeCenter(centerId);
        var user = MakeUser(userId, centerId, center);
        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = new RefreshToken { RefreshTokenId = 1, TokenHash = "hash", ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1), CenterId = centerId, User = user, UserId = userId };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        _mockStore.Setup(s => s.RotateTokenAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.ExecuteAsync("raw", null);

        _mockStore.Verify(s => s.RotateTokenAsync(1, "Rotated", null, _timeProvider.GetUtcNow().UtcDateTime, It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_JwtGenerationThrows_DoesNotCallRotate()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var center = MakeCenter(centerId);
        var user = MakeUser(userId, centerId, center);
        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = new RefreshToken { RefreshTokenId = 1, TokenHash = "hash", ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1), CenterId = centerId, User = user, UserId = userId };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        _mockJwtGen.Setup(g => g.GenerateToken(user, centerId)).Throws(new Exception("JWT Error"));

        await Assert.ThrowsAsync<Exception>(() => _sut.ExecuteAsync("raw", "1.1.1.1"));

        _mockStore.Verify(s => s.RotateTokenAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_ReusedToken_SecondAttemptFails_WithoutSecondSuccessfulRotation()
    {
        var rawToken = "raw-old-token";
        var tokenHash = "hash-old-token";
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockCodec.Setup(c => c.HashToken(rawToken)).Returns(tokenHash);
        _mockStore.Setup(s => s.ResolveCenterIdAsync(tokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var center = MakeCenter(centerId);
        var user = MakeUser(userId, centerId, center);
        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var oldToken = new RefreshToken
        {
            RefreshTokenId = 1,
            TokenHash = tokenHash,
            CenterId = centerId,
            UserId = userId,
            User = user,
            ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(1)
        };

        _mockStore.Setup(s => s.GetTokenAsync(tokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(oldToken);
        _mockJwtGen.Setup(g => g.GenerateToken(user, centerId)).Returns("new-jwt");

        _mockCodec.SetupSequence(c => c.GenerateRawToken())
            .Returns("raw-new-token-1")
            .Returns("raw-new-token-2");
        _mockCodec.Setup(c => c.HashToken("raw-new-token-1")).Returns("hash-new-token-1");
        _mockCodec.Setup(c => c.HashToken("raw-new-token-2")).Returns("hash-new-token-2");

        _mockStore.SetupSequence(s => s.RotateTokenAsync(1, "Rotated", "1.1.1.1", It.IsAny<DateTime>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        // Attempt 1
        var result1 = await _sut.ExecuteAsync(rawToken, "1.1.1.1");
        Assert.True(result1.IsSuccess);

        // Attempt 2
        var result2 = await _sut.ExecuteAsync(rawToken, "1.1.1.1");
        Assert.False(result2.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshInvalid, result2.ErrorCode);

        _mockStore.Verify(s => s.RotateTokenAsync(
            1, "Rotated", "1.1.1.1", It.IsAny<DateTime>(),
            It.IsAny<RefreshToken>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
