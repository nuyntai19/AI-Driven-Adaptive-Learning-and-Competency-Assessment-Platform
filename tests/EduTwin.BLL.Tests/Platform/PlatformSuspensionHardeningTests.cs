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

public sealed class PlatformSuspensionHardeningTests
{
    private static readonly Guid PlatformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
    private static readonly Guid PlatformAdminUserId = Guid.NewGuid();
    private static readonly DateTime FixedUtcNow = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

    private static (EduTwinDbContext Db, PlatformCenterService Service) CreateContext(
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

        var bootstrapper = new AuthorizationBootstrapper(db, mockTimeProvider.Object);
        var mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        mockPasswordHasher
            .Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed_test_password");

        var service = new PlatformCenterService(db, mockTenantContext.Object, mockPasswordHasher.Object, bootstrapper, mockTimeProvider.Object);

        return (db, service);
    }

    [Fact]
    public async Task UpdateCenterStatusAsync_WhenChangingToSuspendedWithoutReason_ReturnsValidationFailed()
    {
        var (db, service) = CreateContext();
        using (db)
        {
            var centerId = Guid.NewGuid();
            var center = new Center
            {
                CenterId = centerId,
                CenterCode = "CENTER_SUSP_1",
                CenterName = "Trung Tam Kiem Tra Suspend",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                RowVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            db.Centers.Add(center);
            await db.SaveChangesAsync();

            var request = new UpdatePlatformCenterStatusRequest
            {
                Status = "Suspended",
                RowVersion = "1",
                Reason = null // Missing reason
            };

            var result = await service.UpdateCenterStatusAsync(centerId, request, "trace-susp-no-reason");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
            Assert.Contains("Lý do", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task UpdateCenterStatusAsync_WhenChangingToSuspendedWithShortReason_ReturnsValidationFailed()
    {
        var (db, service) = CreateContext();
        using (db)
        {
            var centerId = Guid.NewGuid();
            var center = new Center
            {
                CenterId = centerId,
                CenterCode = "CENTER_SUSP_2",
                CenterName = "Trung Tam Kiem Tra Suspend 2",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                RowVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            db.Centers.Add(center);
            await db.SaveChangesAsync();

            var request = new UpdatePlatformCenterStatusRequest
            {
                Status = "Suspended",
                RowVersion = "1",
                Reason = "Abc" // Only 3 characters, must be >= 5
            };

            var result = await service.UpdateCenterStatusAsync(centerId, request, "trace-susp-short-reason");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
            Assert.Contains("Lý do", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task UpdateCenterStatusAsync_WhenChangingToSuspendedWithValidReason_BumpsAuthVersionAndRevokesTokens_AndRecordsAuditCounts()
    {
        var (db, service) = CreateContext();
        using (db)
        {
            var centerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();
            var studentId = Guid.NewGuid();

            var center = new Center
            {
                CenterId = centerId,
                CenterCode = "CENTER_SUSP_3",
                CenterName = "Trung Tam Kiem Tra Suspend 3",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                RowVersion = 1,
                PrimaryManagerUserId = managerId,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            db.Centers.Add(center);

            var manager = new User
            {
                UserId = managerId,
                CenterId = centerId,
                Username = "mgr_susp_3",
                DisplayName = "Manager Susp 3",
                RoleName = UserRole.CenterManager,
                Status = UserStatus.Active,
                AuthVersion = 1,
                PasswordHash = "hash",
                RowVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            var student = new User
            {
                UserId = studentId,
                CenterId = centerId,
                Username = "stu_susp_3",
                DisplayName = "Student Susp 3",
                RoleName = UserRole.Student,
                Status = UserStatus.Active,
                AuthVersion = 3,
                PasswordHash = "hash",
                RowVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            db.Users.AddRange(manager, student);

            var token1 = new RefreshToken
            {
                RefreshTokenId = 301,
                CenterId = centerId,
                UserId = managerId,
                TokenHash = "hash_token_1",
                ExpiresAt = FixedUtcNow.AddDays(7),
                RevokedAt = null,
                CreatedAt = FixedUtcNow
            };
            var token2 = new RefreshToken
            {
                RefreshTokenId = 302,
                CenterId = centerId,
                UserId = studentId,
                TokenHash = "hash_token_2",
                ExpiresAt = FixedUtcNow.AddDays(7),
                RevokedAt = null,
                CreatedAt = FixedUtcNow
            };
            db.RefreshTokens.AddRange(token1, token2);
            await db.SaveChangesAsync();

            var request = new UpdatePlatformCenterStatusRequest
            {
                Status = "Suspended",
                RowVersion = "1",
                Reason = "Tạm ngưng trung tâm để rà soát vi phạm hợp đồng hợp tác giáo dục."
            };

            var result = await service.UpdateCenterStatusAsync(centerId, request, "trace-susp-valid");

            Assert.True(result.IsSuccess);
            Assert.Equal("Suspended", result.Data!.Status);

            // Invariant: All users in center have AuthVersion bumped
            var updatedManager = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == managerId);
            var updatedStudent = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == studentId);
            Assert.Equal(2u, updatedManager.AuthVersion);
            Assert.Equal(4u, updatedStudent.AuthVersion);

            // Invariant: All tokens in center are revoked
            var updatedTokens = await db.RefreshTokens.IgnoreQueryFilters().Where(t => t.CenterId == centerId).ToListAsync();
            Assert.All(updatedTokens, t =>
            {
                Assert.NotNull(t.RevokedAt);
                Assert.Equal("Center suspended by platform administrator.", t.RevokeReason);
            });

            // Verify audit log has affected user and token counts
            var audit = await db.AuthorizationAuditLogs
                .Where(a => a.ActionType == "CenterStatusUpdated" && a.TargetId == centerId.ToString("D"))
                .FirstOrDefaultAsync();
            Assert.NotNull(audit);
            Assert.Contains("\"AffectedUserCount\":2", audit.AfterData!);
            Assert.Contains("\"RevokedTokenCount\":2", audit.AfterData!);
            Assert.Contains("Tạm ngưng trung tâm để rà soát", audit.Reason);
        }
    }

    [Fact]
    public async Task UpdateCenterStatusAsync_WhenReactivatingWithoutActivePrimaryManager_ReturnsValidationFailed()
    {
        var (db, service) = CreateContext();
        using (db)
        {
            var centerId = Guid.NewGuid();
            var center = new Center
            {
                CenterId = centerId,
                CenterCode = "CENTER_SUSP_4",
                CenterName = "Trung Tam Dang Suspended",
                Status = CenterStatus.Suspended,
                Timezone = "Asia/Bangkok",
                RowVersion = 1,
                PrimaryManagerUserId = null, // No primary manager
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            db.Centers.Add(center);
            await db.SaveChangesAsync();

            var request = new UpdatePlatformCenterStatusRequest
            {
                Status = "Active",
                RowVersion = "1",
                Reason = "Kích hoạt lại trung tâm sau khi hoàn thành thanh tra."
            };

            var result = await service.UpdateCenterStatusAsync(centerId, request, "trace-reactivate-no-mgr");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
            Assert.Contains("chưa có Quản lý chính", result.ErrorMessage);
        }
    }
}
