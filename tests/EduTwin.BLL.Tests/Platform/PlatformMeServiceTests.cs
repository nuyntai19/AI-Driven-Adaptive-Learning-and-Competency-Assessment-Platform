using System;
using System.Linq;
using System.Threading.Tasks;
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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Platform;

public sealed class PlatformMeServiceTests
{
    private static readonly Guid PlatformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
    private static readonly Guid PlatformAdminUserId = Guid.NewGuid();
    private static readonly DateTime FixedUtcNow = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

    private static (EduTwinDbContext Db, PlatformMeService Service, Mock<IPasswordHasher<User>> PasswordHasher) CreateContext(
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

        var mockTimeProvider = new Mock<TimeProvider>();
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));

        var mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        mockPasswordHasher
            .Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("new_hashed_password_xyz");
        mockPasswordHasher
            .Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "correct_current_hash", "CurrentP@ssword123"))
            .Returns(PasswordVerificationResult.Success);
        mockPasswordHasher
            .Setup(h => h.VerifyHashedPassword(It.IsAny<User>(), "correct_current_hash", It.Is<string>(p => p != "CurrentP@ssword123")))
            .Returns(PasswordVerificationResult.Failed);

        var service = new PlatformMeService(db, mockTenantContext.Object, mockPasswordHasher.Object, mockTimeProvider.Object);

        return (db, service, mockPasswordHasher);
    }

    private static async Task SeedPlatformAdminUserAsync(EduTwinDbContext db, Guid userId, string passwordHash = "correct_current_hash")
    {
        db.Centers.Add(new Center
        {
            CenterId = PlatformCenterId,
            CenterCode = "PLATFORM",
            CenterName = "EduTwin Platform",
            Status = CenterStatus.Active,
            Timezone = "UTC",
            RowVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });

        db.Users.Add(new User
        {
            UserId = userId,
            CenterId = PlatformCenterId,
            Username = "platform_admin",
            DisplayName = "Platform Administrator",
            RoleName = UserRole.PlatformAdmin,
            Status = UserStatus.Active,
            AuthVersion = 1,
            RowVersion = 1,
            PasswordHash = passwordHash,
            LastLoginAt = FixedUtcNow.AddHours(-1),
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetSecurityProfileAsync_WhenAuthorized_ReturnsProfileAndActiveSessionCount()
    {
        var (db, service, _) = CreateContext();
        using (db)
        {
            await SeedPlatformAdminUserAsync(db, PlatformAdminUserId);

            // Add 2 active refresh tokens and 1 expired/revoked
            db.RefreshTokens.AddRange(
                new RefreshToken
                {
                    RefreshTokenId = 1,
                    CenterId = PlatformCenterId,
                    UserId = PlatformAdminUserId,
                    TokenHash = "hash1",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    RevokedAt = null,
                    CreatedAt = FixedUtcNow
                },
                new RefreshToken
                {
                    RefreshTokenId = 2,
                    CenterId = PlatformCenterId,
                    UserId = PlatformAdminUserId,
                    TokenHash = "hash2",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    RevokedAt = null,
                    CreatedAt = FixedUtcNow
                },
                new RefreshToken
                {
                    RefreshTokenId = 3,
                    CenterId = PlatformCenterId,
                    UserId = PlatformAdminUserId,
                    TokenHash = "hash3",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    RevokedAt = FixedUtcNow.AddHours(-2), // Revoked
                    CreatedAt = FixedUtcNow
                }
            );
            await db.SaveChangesAsync();

            var result = await service.GetSecurityProfileAsync("trace-prof-1");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("platform_admin", result.Data.Username);
            Assert.Equal("Platform Administrator", result.Data.DisplayName);
            Assert.Equal("PlatformAdmin", result.Data.RoleName);
            Assert.Equal(1u, result.Data.AuthVersion);
            Assert.Equal("1", result.Data.RowVersion);
            Assert.Equal(2, result.Data.ActiveSessionCount); // Only 2 active non-revoked
        }
    }

    [Fact]
    public async Task GetSecurityProfileAsync_WhenCallerNotPlatformAdmin_ReturnsForbiddenResource()
    {
        var (db, service, _) = CreateContext(callerRole: nameof(UserRole.CenterManager));
        using (db)
        {
            var result = await service.GetSecurityProfileAsync("trace-prof-forbidden");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenValid_UpdatesPasswordHash_BumpsAuthVersion_AndRevokesTokens()
    {
        var (db, service, _) = CreateContext();
        using (db)
        {
            await SeedPlatformAdminUserAsync(db, PlatformAdminUserId);

            db.RefreshTokens.Add(new RefreshToken
            {
                RefreshTokenId = 10,
                CenterId = PlatformCenterId,
                UserId = PlatformAdminUserId,
                TokenHash = "active_hash_10",
                ExpiresAt = FixedUtcNow.AddDays(7),
                RevokedAt = null,
                CreatedAt = FixedUtcNow
            });
            await db.SaveChangesAsync();

            var request = new PlatformChangePasswordRequest
            {
                CurrentPassword = "CurrentP@ssword123",
                NewPassword = "BrandNewS3curePassword!2026",
                ConfirmPassword = "BrandNewS3curePassword!2026"
            };

            var result = await service.ChangePasswordAsync(request, "trace-pwd-1");

            Assert.True(result.IsSuccess);

            // Invariant verification
            var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == PlatformAdminUserId);
            Assert.Equal("new_hashed_password_xyz", user.PasswordHash);
            Assert.Equal(2u, user.AuthVersion); // AuthVersion bumped
            Assert.Equal(2ul, user.RowVersion); // RowVersion incremented

            // Tokens revoked
            var token = await db.RefreshTokens.IgnoreQueryFilters().FirstAsync(t => t.RefreshTokenId == 10);
            Assert.NotNull(token.RevokedAt);
            Assert.Equal("Password changed by platform administrator.", token.RevokeReason);

            // Redacted audit log
            var audit = await db.AuthorizationAuditLogs
                .Where(a => a.ActionType == "PlatformAdminPasswordChanged" && a.TargetId == PlatformAdminUserId.ToString("D"))
                .FirstOrDefaultAsync();
            Assert.NotNull(audit);
            Assert.Null(audit.BeforeData); // Zero passwords logged
            Assert.Contains("\"AuthVersion\":2", audit.AfterData!);
        }
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenWrongCurrentPassword_ReturnsInvalidCredentials()
    {
        var (db, service, _) = CreateContext();
        using (db)
        {
            await SeedPlatformAdminUserAsync(db, PlatformAdminUserId);

            var request = new PlatformChangePasswordRequest
            {
                CurrentPassword = "WrongPassword999!",
                NewPassword = "BrandNewS3curePassword!2026",
                ConfirmPassword = "BrandNewS3curePassword!2026"
            };

            var result = await service.ChangePasswordAsync(request, "trace-pwd-wrong");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenPasswordTooShort_ReturnsValidationFailed()
    {
        var (db, service, _) = CreateContext();
        using (db)
        {
            await SeedPlatformAdminUserAsync(db, PlatformAdminUserId);

            var request = new PlatformChangePasswordRequest
            {
                CurrentPassword = "CurrentP@ssword123",
                NewPassword = "short", // < 12 chars
                ConfirmPassword = "short"
            };

            var result = await service.ChangePasswordAsync(request, "trace-pwd-short");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ChangePasswordAsync_WhenConfirmPasswordMismatch_ReturnsValidationFailed()
    {
        var (db, service, _) = CreateContext();
        using (db)
        {
            await SeedPlatformAdminUserAsync(db, PlatformAdminUserId);

            var request = new PlatformChangePasswordRequest
            {
                CurrentPassword = "CurrentP@ssword123",
                NewPassword = "BrandNewS3curePassword!2026",
                ConfirmPassword = "MismatchPassword!2026"
            };

            var result = await service.ChangePasswordAsync(request, "trace-pwd-mismatch");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        }
    }

    [Fact]
    public async Task RevokeSessionsAsync_BumpsAuthVersion_RevokesAllTokens_AndLogsAudit()
    {
        var (db, service, _) = CreateContext();
        using (db)
        {
            await SeedPlatformAdminUserAsync(db, PlatformAdminUserId);

            db.RefreshTokens.AddRange(
                new RefreshToken
                {
                    RefreshTokenId = 21,
                    CenterId = PlatformCenterId,
                    UserId = PlatformAdminUserId,
                    TokenHash = "active_hash_21",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    RevokedAt = null,
                    CreatedAt = FixedUtcNow
                },
                new RefreshToken
                {
                    RefreshTokenId = 22,
                    CenterId = PlatformCenterId,
                    UserId = PlatformAdminUserId,
                    TokenHash = "active_hash_22",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    RevokedAt = null,
                    CreatedAt = FixedUtcNow
                }
            );
            await db.SaveChangesAsync();

            var request = new PlatformRevokeSessionsRequest
            {
                Reason = "Phát hiện thiết bị lạ đăng nhập, chủ động thu hồi tất cả phiên."
            };

            var result = await service.RevokeSessionsAsync(request, "trace-revoke-all");

            Assert.True(result.IsSuccess);

            // User verification
            var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == PlatformAdminUserId);
            Assert.Equal(2u, user.AuthVersion);
            Assert.Equal(2ul, user.RowVersion);

            // All tokens revoked
            var tokens = await db.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == PlatformAdminUserId).ToListAsync();
            Assert.All(tokens, t =>
            {
                Assert.NotNull(t.RevokedAt);
                Assert.Equal("Phát hiện thiết bị lạ đăng nhập, chủ động thu hồi tất cả phiên.", t.RevokeReason);
            });

            // Audit verification
            var audit = await db.AuthorizationAuditLogs
                .Where(a => a.ActionType == "PlatformAdminSessionsRevoked" && a.TargetId == PlatformAdminUserId.ToString("D"))
                .FirstOrDefaultAsync();
            Assert.NotNull(audit);
            Assert.Contains("\"RevokedTokenCount\":2", audit.AfterData!);
            Assert.Equal("Phát hiện thiết bị lạ đăng nhập, chủ động thu hồi tất cả phiên.", audit.Reason);
        }
    }

    [Fact]
    public async Task RevokeSessionsAsync_WhenReasonContainsTokenOrPassword_ReturnsValidationFailed()
    {
        var (db, service, _) = CreateContext();
        using (db)
        {
            await SeedPlatformAdminUserAsync(db, PlatformAdminUserId);

            var request = new PlatformRevokeSessionsRequest
            {
                Reason = "Revoking sessions due to leaked token: abc123def456"
            };

            var result = await service.RevokeSessionsAsync(request, "trace-revoke-secret");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
            Assert.Contains("thông tin nhạy cảm", result.ErrorMessage);
        }
    }
}
