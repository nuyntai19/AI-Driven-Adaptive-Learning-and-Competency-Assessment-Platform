using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class LogoutUseCaseTests
{
    private readonly Mock<IRefreshTokenCodec> _mockCodec;
    private readonly Mock<IRefreshTokenStore> _mockStore;
    private readonly Mock<IBackgroundTenantScopeFactory> _mockScopeFactory;
    private readonly FixedTimeProvider _timeProvider;
    private readonly LogoutUseCase _sut;

    public LogoutUseCaseTests()
    {
        _mockCodec = new Mock<IRefreshTokenCodec>();
        _mockStore = new Mock<IRefreshTokenStore>();
        _mockScopeFactory = new Mock<IBackgroundTenantScopeFactory>();
        _timeProvider = new FixedTimeProvider(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _mockScopeFactory.Setup(x => x.BeginScope(It.IsAny<Guid>()))
            .Returns(Mock.Of<IDisposable>());

        _mockCodec.Setup(c => c.IsValidRawToken(It.IsAny<string>())).Returns(true);

        _sut = new LogoutUseCase(_mockCodec.Object, _mockStore.Object, _mockScopeFactory.Object, _timeProvider);
    }

    [Fact]
    public async Task Logout_ValidToken_CallsRevokeToken()
    {
        var rawToken = "raw";
        var tokenHash = "hash";
        var centerId = Guid.NewGuid();

        _mockCodec.Setup(c => c.HashToken(rawToken)).Returns(tokenHash);
        _mockStore.Setup(s => s.ResolveCenterIdAsync(tokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var token = new RefreshToken { RefreshTokenId = 1, TokenHash = tokenHash, CenterId = centerId, User = new User { CenterId = centerId } };
        _mockStore.Setup(s => s.GetTokenAsync(tokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(token);

        await _sut.ExecuteAsync(rawToken, "1.1.1.1");

        _mockStore.Verify(s => s.RevokeTokenAsync(1, "Logout", "1.1.1.1", _timeProvider.GetUtcNow().UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_MalformedToken_DoesNotLookup()
    {
        _mockCodec.Setup(c => c.IsValidRawToken(It.IsAny<string>())).Returns(false);
        await _sut.ExecuteAsync("invalid", "1.1.1.1");

        _mockStore.Verify(s => s.ResolveCenterIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Logout_NullToken_DoesNotHashOrLookup()
    {
        await _sut.ExecuteAsync(null, "1.1.1.1");

        _mockStore.Verify(s => s.ResolveCenterIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCodec.Verify(c => c.HashToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Logout_UnknownToken_DoesNothingAndDoesNotThrow()
    {
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        await _sut.ExecuteAsync("raw", "1.1.1.1");

        _mockStore.Verify(s => s.GetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Logout_AlreadyRevokedToken_DoesNothingAndDoesNotThrow()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var token = new RefreshToken { RefreshTokenId = 1, TokenHash = "hash", RevokedAt = _timeProvider.GetUtcNow().UtcDateTime };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        await _sut.ExecuteAsync("raw", "1.1.1.1");

        _mockStore.Verify(s => s.RevokeTokenAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Logout_CrossTenantMismatch_DoesNotRevoke()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var token = new RefreshToken { RefreshTokenId = 1, TokenHash = "hash", CenterId = Guid.NewGuid(), User = new User { CenterId = centerId } };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        await _sut.ExecuteAsync("raw", "1.1.1.1");

        _mockStore.Verify(s => s.RevokeTokenAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Logout_NullClientIp_PassedAsNull()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var token = new RefreshToken { RefreshTokenId = 1, TokenHash = "hash", CenterId = centerId, User = new User { CenterId = centerId } };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        await _sut.ExecuteAsync("raw", null);

        _mockStore.Verify(s => s.RevokeTokenAsync(1, "Logout", null, _timeProvider.GetUtcNow().UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_RevokeAffected0_RemainsSafe()
    {
        var centerId = Guid.NewGuid();
        _mockCodec.Setup(c => c.HashToken("raw")).Returns("hash");
        _mockStore.Setup(s => s.ResolveCenterIdAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(centerId);

        var token = new RefreshToken { RefreshTokenId = 1, TokenHash = "hash", CenterId = centerId, User = new User { CenterId = centerId } };
        _mockStore.Setup(s => s.GetTokenAsync("hash", It.IsAny<CancellationToken>())).ReturnsAsync(token);

        _mockStore.Setup(s => s.RevokeTokenAsync(1, "Logout", "1.1.1.1", _timeProvider.GetUtcNow().UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _sut.ExecuteAsync("raw", "1.1.1.1");

        // Assert: execution completed without throwing
        Assert.True(true);
    }
}
