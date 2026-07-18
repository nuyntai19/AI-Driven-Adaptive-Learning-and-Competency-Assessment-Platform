using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class LoginUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<IBackgroundTenantScopeFactory> _mockScopeFactory;
    private readonly Mock<IPasswordHasher<User>> _mockHasher;
    private readonly Mock<IJwtTokenGenerator> _mockTokenGenerator;
    private readonly FixedTimeProvider _timeProvider;
    private readonly LoginUseCase _sut;

    public LoginUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
        _mockScopeFactory = new Mock<IBackgroundTenantScopeFactory>();

        _mockScopeFactory.Setup(x => x.BeginScope(It.IsAny<Guid>()))
            .Returns<Guid>(centerId =>
            {
                mockAccessor.Setup(a => a.CenterId).Returns(centerId);
                var mockDisposable = new Mock<IDisposable>();
                mockDisposable.Setup(d => d.Dispose()).Callback(() => mockAccessor.Setup(a => a.CenterId).Returns(Guid.Empty));
                return mockDisposable.Object;
            });

        _mockHasher = new Mock<IPasswordHasher<User>>();
        _mockTokenGenerator = new Mock<IJwtTokenGenerator>();
        _timeProvider = new FixedTimeProvider(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);

        _mockTokenGenerator.Setup(g => g.GenerateToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns("mock-jwt-token");

        _sut = new LoginUseCase(_dbContext, _mockScopeFactory.Object, _mockHasher.Object, _mockTokenGenerator.Object, _timeProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static Center MakeCenter(Guid centerId, string code = "C1", CenterStatus status = CenterStatus.Active) =>
        new() { CenterId = centerId, CenterCode = code, CenterName = $"Center {code}", Status = status, Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static User MakeUser(Guid userId, Guid centerId, Center center, string username = "manager", UserRole role = UserRole.CenterManager, UserStatus status = UserStatus.Active, bool isDeleted = false) =>
        new() { UserId = userId, CenterId = centerId, Username = username, PasswordHash = "hash", RoleName = role, DisplayName = "Display", Status = status, IsDeleted = isDeleted, Center = center, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    [Fact]
    public async Task Login_Success_ReturnsTokenAndCookieData()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var userId = Guid.NewGuid();
        var user = MakeUser(userId, centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        var result = await _sut.ExecuteAsync(request, "1.1.1.1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("mock-jwt-token", result.Data.AccessToken);
        Assert.Equal("Bearer", result.Data.TokenType);
        Assert.Equal(900, result.Data.ExpiresInSeconds);
        Assert.Equal("manager", result.Data.User.Username);
        Assert.NotNull(result.RawRefreshToken);
        Assert.NotNull(result.RefreshTokenExpiresAt);

        var tokenInDb = await _dbContext.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync();
        Assert.NotNull(tokenInDb);
        Assert.Equal(userId, tokenInDb.UserId);
        Assert.Equal("1.1.1.1", tokenInDb.CreatedByIp);
    }

    [Fact]
    public async Task Login_UnknownCenter_ReturnsInvalidCredentials()
    {
        var request = new LoginRequest { CenterCode = "UNKNOWN", Username = "manager", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
    }

    [Fact]
    public async Task Login_UnknownUsername_ReturnsInvalidCredentials()
    {
        var centerId = Guid.NewGuid();
        _dbContext.Centers.Add(MakeCenter(centerId));
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "unknown", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsInvalidCredentials()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Failed);

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "wrong" };
        var result = await _sut.ExecuteAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
    }

    [Theory]
    [InlineData(CenterStatus.Suspended, UserStatus.Active, ErrorCodes.AuthUserDisabled)]
    [InlineData(CenterStatus.Active, UserStatus.Locked, ErrorCodes.AuthUserDisabled)]
    [InlineData(CenterStatus.Active, UserStatus.Disabled, ErrorCodes.AuthUserDisabled)]
    public async Task Login_DisabledOrSuspended_ReturnsDisabledError(CenterStatus centerStatus, UserStatus userStatus, string expectedError)
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId, status: centerStatus);
        var user = MakeUser(Guid.NewGuid(), centerId, center, status: userStatus);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.ErrorCode);
    }

    [Fact]
    public async Task Login_SoftDeletedUser_ReturnsInvalidCredentials()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center, isDeleted: true);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
    }

    [Fact]
    public async Task SameUsernameInDifferentCenters_DoesNotLeak()
    {
        var centerA = MakeCenter(Guid.NewGuid(), "CA");
        var centerB = MakeCenter(Guid.NewGuid(), "CB");

        var userA = MakeUser(Guid.NewGuid(), centerA.CenterId, centerA, username: "admin");
        var userB = MakeUser(Guid.NewGuid(), centerB.CenterId, centerB, username: "admin");

        _dbContext.Centers.AddRange(centerA, centerB);
        _dbContext.Users.AddRange(userA, userB);
        await _dbContext.SaveChangesAsync();

        var requestB = new LoginRequest { CenterCode = "CB", Username = "admin", Password = "password" };
        var resultB = await _sut.ExecuteAsync(requestB, null);

        Assert.True(resultB.IsSuccess);
        Assert.Equal(userB.UserId.ToString("D").ToLowerInvariant(), resultB.Data!.User.UserId);
        Assert.Equal(centerB.CenterId.ToString("D").ToLowerInvariant(), resultB.Data!.User.CenterId);
    }

    [Fact]
    public async Task Login_SuspendedCenterWrongPassword_ReturnsInvalidCredentials()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId, status: CenterStatus.Suspended);
        var user = MakeUser(Guid.NewGuid(), centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Failed);

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "wrong" };
        var result = await _sut.ExecuteAsync(request, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
    }

    [Fact]
    public async Task Login_SuccessRehashNeeded_UpdatesPasswordHash()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center);
        user.PasswordHash = "old_hash";

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _mockHasher.Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);
        _mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("new_hash");

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        await _sut.ExecuteAsync(request, null);

        var userInDb = await _dbContext.Users.IgnoreQueryFilters().FirstAsync();
        Assert.Equal("new_hash", userInDb.PasswordHash);
    }

    [Fact]
    public async Task Login_JwtGenerationFails_DoesNotSaveChanges()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _mockTokenGenerator.Setup(g => g.GenerateToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Throws(new InvalidOperationException("JWT Fail"));

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExecuteAsync(request, null));

        var tokenInDb = await _dbContext.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync();
        Assert.Null(tokenInDb);
    }

    [Theory]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    public async Task Login_OtherRoles_Success(UserRole role)
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center, username: "user", role: role);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "user", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        Assert.True(result.IsSuccess);
        _mockTokenGenerator.Verify(g => g.GenerateToken(It.Is<User>(u => u.UserId == user.UserId), centerId), Times.Once);
    }

    [Fact]
    public async Task Login_RefreshTokenHash_IsLowercaseSha256Hex_64Chars()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        var tokenInDb = await _dbContext.RefreshTokens.IgnoreQueryFilters().FirstAsync();

        Assert.Equal(64, tokenInDb.TokenHash.Length);
        Assert.Equal(tokenInDb.TokenHash, tokenInDb.TokenHash.ToLowerInvariant());
        Assert.True(tokenInDb.TokenHash.All(c => "0123456789abcdef".Contains(c)));
    }

    [Fact]
    public async Task Login_Sha256OfRawToken_EqualsStoredHash()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        var rawBytes = Encoding.UTF8.GetBytes(result.RawRefreshToken!);
        var expectedHash = Convert.ToHexString(SHA256.HashData(rawBytes)).ToLowerInvariant();

        var tokenInDb = await _dbContext.RefreshTokens.IgnoreQueryFilters().FirstAsync();
        Assert.Equal(expectedHash, tokenInDb.TokenHash);
    }

    [Fact]
    public async Task Login_RawRefreshToken_NotStoredInDatabase()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        var tokenInDb = await _dbContext.RefreshTokens.IgnoreQueryFilters().FirstAsync();
        Assert.NotEqual(result.RawRefreshToken, tokenInDb.TokenHash);
    }

    [Fact]
    public async Task Login_LastLoginAt_MatchesFixedTimeProvider()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        await _sut.ExecuteAsync(request, null);

        var userInDb = await _dbContext.Users.IgnoreQueryFilters().FirstAsync();
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), userInDb.LastLoginAt);
    }

    [Fact]
    public async Task Login_ExpiresAt_Is30DaysFromNow()
    {
        var centerId = Guid.NewGuid();
        var center = MakeCenter(centerId);
        var user = MakeUser(Guid.NewGuid(), centerId, center);

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest { CenterCode = "C1", Username = "manager", Password = "password" };
        var result = await _sut.ExecuteAsync(request, null);

        var fixedNow = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(fixedNow.AddDays(30), result.RefreshTokenExpiresAt);

        var tokenInDb = await _dbContext.RefreshTokens.IgnoreQueryFilters().FirstAsync();
        Assert.Equal(30, (tokenInDb.ExpiresAt - tokenInDb.CreatedAt).TotalDays);
    }

    [Fact]
    public void LoginResponse_DoesNotExposeRawRefreshToken()
    {
        var properties = typeof(LoginResponse).GetProperties();
        var propertyNames = properties.Select(p => p.Name).ToArray();

        Assert.DoesNotContain("RawRefreshToken", propertyNames);
        Assert.DoesNotContain("RefreshToken", propertyNames);

        var dataProperties = typeof(LoginDataDto).GetProperties();
        var dataPropertyNames = dataProperties.Select(p => p.Name).ToArray();

        Assert.DoesNotContain("RawRefreshToken", dataPropertyNames);
        Assert.DoesNotContain("RefreshToken", dataPropertyNames);
    }
}
