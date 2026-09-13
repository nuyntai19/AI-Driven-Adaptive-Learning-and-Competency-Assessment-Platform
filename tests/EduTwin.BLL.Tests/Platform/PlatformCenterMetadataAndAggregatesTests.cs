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

public sealed class PlatformCenterMetadataAndAggregatesTests
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
    public async Task UpdateCenterMetadata_WhenValid_UpdatesNameAndTimezone_AndIncrementsRowVersion()
    {
        var (db, service) = CreateContext();
        using (db)
        {
            var centerId = Guid.NewGuid();
            var managerId = Guid.NewGuid();

            var center = new Center
            {
                CenterId = centerId,
                CenterCode = "ALPHA",
                CenterName = "Trung Tam Alpha Ban Dau",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                RowVersion = 1,
                PrimaryManagerUserId = managerId,
                CreatedAt = FixedUtcNow.AddDays(-10),
                UpdatedAt = FixedUtcNow.AddDays(-10)
            };

            var manager = new User
            {
                UserId = managerId,
                CenterId = centerId,
                Username = "manager_alpha",
                DisplayName = "Quan Ly Alpha",
                RoleName = UserRole.CenterManager,
                Status = UserStatus.Active,
                PasswordHash = "hash",
                RowVersion = 1,
                CreatedAt = FixedUtcNow.AddDays(-10),
                UpdatedAt = FixedUtcNow.AddDays(-10)
            };

            db.Centers.Add(center);
            db.Users.Add(manager);
            await db.SaveChangesAsync();

            var request = new UpdateCenterMetadataRequest
            {
                CenterName = "Trung Tam Alpha Moi Cap Nhat",
                Timezone = "Asia/Ho_Chi_Minh",
                ExpectedRowVersion = "1",
                Reason = "Cập nhật tên và múi giờ theo quyết định tái cơ cấu"
            };

            var result = await service.UpdateCenterMetadataAsync(centerId, request, "trace-meta-1");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Trung Tam Alpha Moi Cap Nhat", result.Data.CenterName);
            Assert.Equal("Asia/Ho_Chi_Minh", result.Data.Timezone);
            Assert.Equal("2", result.Data.RowVersion);
            Assert.Equal("ALPHA", result.Data.CenterCode); // Immutable invariant
            Assert.Equal(centerId, result.Data.CenterId); // Immutable invariant

            // Verify audit log
            var audit = await db.AuthorizationAuditLogs
                .Where(a => a.ActionType == "CenterMetadataUpdated" && a.TargetId == centerId.ToString("D"))
                .FirstOrDefaultAsync();

            Assert.NotNull(audit);
            Assert.Equal(centerId, audit.TargetCenterId);
            Assert.Equal(PlatformCenterId, audit.CenterId);
            Assert.Contains("Trung Tam Alpha Ban Dau", audit.BeforeData!);
            Assert.Contains("Trung Tam Alpha Moi Cap Nhat", audit.AfterData!);
        }
    }

    [Fact]
    public async Task UpdateCenterMetadata_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        var (db, service) = CreateContext();
        using (db)
        {
            var centerId = Guid.NewGuid();
            var center = new Center
            {
                CenterId = centerId,
                CenterCode = "BETA",
                CenterName = "Trung Tam Beta",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                RowVersion = 3,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            db.Centers.Add(center);
            await db.SaveChangesAsync();

            var request = new UpdateCenterMetadataRequest
            {
                CenterName = "Trung Tam Beta Mới",
                ExpectedRowVersion = "2", // Stale version
                Reason = "Thu cap nhat stale OCC"
            };

            var result = await service.UpdateCenterMetadataAsync(centerId, request, "trace-meta-stale");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        }
    }

    [Fact]
    public async Task UpdateCenterMetadata_WhenTargetIsRootTenantPlatform_ReturnsForbiddenResource()
    {
        var (db, service) = CreateContext();
        using (db)
        {
            var request = new UpdateCenterMetadataRequest
            {
                CenterName = "Platform Alteration Attempt",
                ExpectedRowVersion = "1",
                Reason = "Test privilege barrier"
            };

            var result = await service.UpdateCenterMetadataAsync(PlatformCenterId, request, "trace-plat-barrier");

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
        }
    }

    [Fact]
    public async Task SafeAggregates_CalculatesAccurateCounts_AndDoesNotLeakCrossTenantData()
    {
        var (db, service) = CreateContext();
        using (db)
        {
            var centerA = Guid.NewGuid();
            var centerB = Guid.NewGuid();

            var managerA = Guid.NewGuid();
            var managerB = Guid.NewGuid();

            // Center A setup
            db.Centers.Add(new Center
            {
                CenterId = centerA,
                CenterCode = "CENTER_A",
                CenterName = "Trung Tam A",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                RowVersion = 1,
                PrimaryManagerUserId = managerA,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });

            // Center B setup
            db.Centers.Add(new Center
            {
                CenterId = centerB,
                CenterCode = "CENTER_B",
                CenterName = "Trung Tam B",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                RowVersion = 1,
                PrimaryManagerUserId = null, // No primary manager
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });

            // Primary Manager A (Active)
            db.Users.Add(new User
            {
                UserId = managerA,
                CenterId = centerA,
                Username = "mgr_a",
                DisplayName = "Manager A",
                RoleName = UserRole.CenterManager,
                Status = UserStatus.Active,
                PasswordHash = "hash",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });

            // Secondary Manager A (Active)
            db.Users.Add(new User
            {
                UserId = Guid.NewGuid(),
                CenterId = centerA,
                Username = "mgr_a2",
                DisplayName = "Manager A2",
                RoleName = UserRole.CenterManager,
                Status = UserStatus.Active,
                PasswordHash = "hash",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });

            // 3 Students in Center A: 2 Active, 1 Suspended, 1 Soft-deleted
            db.Users.AddRange(
                new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = centerA,
                    Username = "stu_a1",
                    DisplayName = "Student A1",
                    RoleName = UserRole.Student,
                    Status = UserStatus.Active,
                    PasswordHash = "hash",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = centerA,
                    Username = "stu_a2",
                    DisplayName = "Student A2",
                    RoleName = UserRole.Student,
                    Status = UserStatus.Active,
                    PasswordHash = "hash",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = centerA,
                    Username = "stu_a_suspended",
                    DisplayName = "Student Suspended",
                    RoleName = UserRole.Student,
                    Status = UserStatus.Locked, // MUST NOT BE COUNTED
                    PasswordHash = "hash",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = centerA,
                    Username = "stu_a_deleted",
                    DisplayName = "Student Deleted",
                    RoleName = UserRole.Student,
                    Status = UserStatus.Active,
                    PasswordHash = "hash",
                    IsDeleted = true, // MUST NOT BE COUNTED
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );

            // 2 Teachers in Center A: 1 Active, 1 Suspended
            db.Users.AddRange(
                new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = centerA,
                    Username = "tea_a1",
                    DisplayName = "Teacher A1",
                    RoleName = UserRole.Teacher,
                    Status = UserStatus.Active,
                    PasswordHash = "hash",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = centerA,
                    Username = "tea_a_suspended",
                    DisplayName = "Teacher Suspended",
                    RoleName = UserRole.Teacher,
                    Status = UserStatus.Locked, // MUST NOT BE COUNTED
                    PasswordHash = "hash",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );

            // Classes in Center A: 2 Active, 1 Deleted
            db.Classes.AddRange(
                new Class
                {
                    ClassId = Guid.NewGuid(),
                    CenterId = centerA,
                    ClassName = "Lop 10A1",
                    AcademicYear = "2025-2026",
                    Status = ClassStatus.Active,
                    IsDeleted = false,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Class
                {
                    ClassId = Guid.NewGuid(),
                    CenterId = centerA,
                    ClassName = "Lop 10A2",
                    AcademicYear = "2025-2026",
                    Status = ClassStatus.Active,
                    IsDeleted = false,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                },
                new Class
                {
                    ClassId = Guid.NewGuid(),
                    CenterId = centerA,
                    ClassName = "Lop Da Xoa",
                    AcademicYear = "2025-2026",
                    Status = ClassStatus.Active,
                    IsDeleted = true, // MUST NOT BE COUNTED
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                }
            );

            // Center B users (should not leak into Center A counts)
            db.Users.Add(new User
            {
                UserId = managerB,
                CenterId = centerB,
                Username = "mgr_b",
                DisplayName = "Manager B",
                RoleName = UserRole.CenterManager,
                Status = UserStatus.Active,
                PasswordHash = "hash",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });
            db.Users.Add(new User
            {
                UserId = Guid.NewGuid(),
                CenterId = centerB,
                Username = "stu_b1",
                DisplayName = "Student B1",
                RoleName = UserRole.Student,
                Status = UserStatus.Active,
                PasswordHash = "hash",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            });

            await db.SaveChangesAsync();

            // List centers to test batch safe aggregates
            var listResult = await service.ListCentersAsync(1, 20, null, null, "trace-list-agg");

            Assert.True(listResult.IsSuccess);
            Assert.NotNull(listResult.Data);
            Assert.Equal(2, listResult.Data.TotalCount);

            var itemA = listResult.Data.Items.Single(c => c.CenterId == centerA);
            Assert.Equal(2, itemA.ActiveStudentCount); // Exactly 2 active students
            Assert.Equal(1, itemA.ActiveTeacherCount); // Exactly 1 active teacher
            Assert.Equal(2, itemA.ClassCount);         // Exactly 2 active classes
            Assert.Equal(2, itemA.ActiveManagerCount); // Exactly 2 active managers
            Assert.True(itemA.HasActivePrimaryManager); // Primary manager is Active

            var itemB = listResult.Data.Items.Single(c => c.CenterId == centerB);
            Assert.Equal(1, itemB.ActiveStudentCount);
            Assert.Equal(0, itemB.ActiveTeacherCount);
            Assert.Equal(0, itemB.ClassCount);
            Assert.Equal(1, itemB.ActiveManagerCount);
            Assert.False(itemB.HasActivePrimaryManager); // Has no primary manager assigned
        }
    }
}
