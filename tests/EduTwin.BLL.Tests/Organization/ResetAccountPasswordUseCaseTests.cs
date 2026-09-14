using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class ResetAccountPasswordUseCaseTests
{
    private readonly Guid _centerId = Guid.Parse("e331c1f3-18d2-43bb-a5a4-1507dfbb7d90");
    private readonly Guid _managerId = Guid.Parse("84f04c63-4402-4fc9-b6eb-bf89bc5f4923");
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IPasswordHasher<User>> _mockPasswordHasher;
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    public ResetAccountPasswordUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_managerId);
        _mockTenantContext.Setup(c => c.Role).Returns("CenterManager");

        _mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        _mockPasswordHasher.Setup(p => p.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed_new_password_123");
    }

    private (EduTwinDbContext, ThrowingSaveChangesInterceptor) CreateContext(string dbName)
    {
        var interceptor = new ThrowingSaveChangesInterceptor();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);
        return (new EduTwinDbContext(options, mockAccessor.Object), interceptor);
    }

    private class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public Action? OnSavingChangesAsyncAction { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            OnSavingChangesAsyncAction?.Invoke();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private async Task SeedCenterAsync(EduTwinDbContext context, Guid? centerId = null)
    {
        var cid = centerId ?? _centerId;
        if (!await context.Centers.AnyAsync(c => c.CenterId == cid))
        {
            context.Centers.Add(new Center
            {
                CenterId = cid,
                CenterName = "Test Center",
                CenterCode = "TC",
                Timezone = "Asia/Ho_Chi_Minh",
                Status = CenterStatus.Active,
                IsDeleted = false,
                CreatedAt = _fixedTime.UtcDateTime,
                UpdatedAt = _fixedTime.UtcDateTime
            });
            await context.SaveChangesAsync();
        }
    }

    private async Task SeedTeacherAsync(EduTwinDbContext context, Guid teacherId, ulong rowVersion = 10, Guid? centerId = null)
    {
        var cid = centerId ?? _centerId;
        await SeedCenterAsync(context, cid);

        context.Users.Add(new User
        {
            UserId = teacherId,
            CenterId = cid,
            Username = $"teacher_{teacherId:N}",
            PasswordHash = "old_hash",
            RoleName = UserRole.Teacher,
            DisplayName = "Test Teacher",
            Status = UserStatus.Active,
            IsDeleted = false,
            AuthVersion = 1,
            RowVersion = rowVersion,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        context.Teachers.Add(new Teacher
        {
            TeacherId = teacherId,
            CenterId = cid,
            Department = "Math",
            IsDeleted = false,
            RowVersion = rowVersion,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        await context.SaveChangesAsync();
    }

    private async Task SeedStudentAsync(EduTwinDbContext context, Guid studentId, ulong rowVersion = 10, Guid? centerId = null)
    {
        var cid = centerId ?? _centerId;
        await SeedCenterAsync(context, cid);

        context.Users.Add(new User
        {
            UserId = studentId,
            CenterId = cid,
            Username = $"student_{studentId:N}",
            PasswordHash = "old_hash",
            RoleName = UserRole.Student,
            DisplayName = "Test Student",
            Status = UserStatus.Active,
            IsDeleted = false,
            AuthVersion = 1,
            RowVersion = rowVersion,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        context.Students.Add(new Student
        {
            StudentId = studentId,
            CenterId = cid,
            FullName = "Test Student",
            GradeLevel = 10,
            IsDeleted = false,
            RowVersion = rowVersion,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData("CenterManager")]
    [InlineData("PlatformAdmin")]
    [InlineData("InvalidRole")]
    public async Task ExecuteAsync_TargetRoleInvalidOrRestricted_ReturnsResourceNotFound(string invalidTargetRole)
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var targetId = Guid.NewGuid();

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new ResetAccountPasswordUseCase(
            context,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            timeProvider.Object,
            NullLogger<ResetAccountPasswordUseCase>.Instance);

        var request = new ResetAccountPasswordRequest
        {
            NewPassword = "ValidPassword123!",
            ExpectedUserRowVersion = "10",
            Reason = "Admin password reset"
        };

        var result = await useCase.ExecuteAsync(targetId, invalidTargetRole, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("12345678901")] // 11 chars (< 12)
    public async Task ExecuteAsync_NewPasswordInvalid_ReturnsValidationFailed(string newPassword)
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var teacherId = Guid.NewGuid();
        await SeedTeacherAsync(context, teacherId);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new ResetAccountPasswordUseCase(
            context,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            timeProvider.Object,
            NullLogger<ResetAccountPasswordUseCase>.Instance);

        var request = new ResetAccountPasswordRequest
        {
            NewPassword = newPassword,
            ExpectedUserRowVersion = "10",
            Reason = "Admin password reset"
        };

        var result = await useCase.ExecuteAsync(teacherId, nameof(UserRole.Teacher), request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task ExecuteAsync_ExpectedRowVersionInvalid_ReturnsValidationFailed(string expectedRowVersion)
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var teacherId = Guid.NewGuid();
        await SeedTeacherAsync(context, teacherId);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new ResetAccountPasswordUseCase(
            context,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            timeProvider.Object,
            NullLogger<ResetAccountPasswordUseCase>.Instance);

        var request = new ResetAccountPasswordRequest
        {
            NewPassword = "ValidPassword123!",
            ExpectedUserRowVersion = expectedRowVersion,
            Reason = "Admin password reset"
        };

        var result = await useCase.ExecuteAsync(teacherId, nameof(UserRole.Teacher), request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")] // < 5 chars
    public async Task ExecuteAsync_ReasonInvalid_ReturnsValidationFailed(string reason)
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var teacherId = Guid.NewGuid();
        await SeedTeacherAsync(context, teacherId);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new ResetAccountPasswordUseCase(
            context,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            timeProvider.Object,
            NullLogger<ResetAccountPasswordUseCase>.Instance);

        var request = new ResetAccountPasswordRequest
        {
            NewPassword = "ValidPassword123!",
            ExpectedUserRowVersion = "10",
            Reason = reason
        };

        var result = await useCase.ExecuteAsync(teacherId, nameof(UserRole.Teacher), request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantTarget_ReturnsResourceNotFound()
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var teacherId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        await SeedTeacherAsync(context, teacherId, centerId: otherCenterId);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new ResetAccountPasswordUseCase(
            context,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            timeProvider.Object,
            NullLogger<ResetAccountPasswordUseCase>.Instance);

        var request = new ResetAccountPasswordRequest
        {
            NewPassword = "ValidPassword123!",
            ExpectedUserRowVersion = "10",
            Reason = "Admin password reset"
        };

        var result = await useCase.ExecuteAsync(teacherId, nameof(UserRole.Teacher), request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_RowVersionMismatch_ReturnsConcurrencyConflict()
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var teacherId = Guid.NewGuid();
        await SeedTeacherAsync(context, teacherId, rowVersion: 15);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new ResetAccountPasswordUseCase(
            context,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            timeProvider.Object,
            NullLogger<ResetAccountPasswordUseCase>.Instance);

        var request = new ResetAccountPasswordRequest
        {
            NewPassword = "ValidPassword123!",
            ExpectedUserRowVersion = "10", // Mismatch: actual is 15
            Reason = "Admin password reset"
        };

        var result = await useCase.ExecuteAsync(teacherId, nameof(UserRole.Teacher), request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherSuccess_UpdatesPassword_BumpsVersions_RevokesTokens_WritesRedactedAudit()
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var teacherId = Guid.NewGuid();
        await SeedTeacherAsync(context, teacherId, rowVersion: 10);

        context.RefreshTokens.Add(new RefreshToken
        {
            CenterId = _centerId,
            UserId = teacherId,
            TokenHash = "token_hash_teacher",
            ExpiresAt = _fixedTime.UtcDateTime.AddDays(7),
            CreatedAt = _fixedTime.UtcDateTime
        });
        await context.SaveChangesAsync();

        var timeProvider = new Mock<TimeProvider>();
        var now = _fixedTime.AddMinutes(30);
        timeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var useCase = new ResetAccountPasswordUseCase(
            context,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            timeProvider.Object,
            NullLogger<ResetAccountPasswordUseCase>.Instance);

        var request = new ResetAccountPasswordRequest
        {
            NewPassword = "SuperSecurePassword123!",
            ExpectedUserRowVersion = "1",
            Reason = "Khôi phục mật khẩu định kỳ theo yêu cầu giáo viên"
        };

        var result = await useCase.ExecuteAsync(teacherId, nameof(UserRole.Teacher), request, "trace-teacher-pwd");

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(teacherId.ToString("D"), result.TargetUserId);
        Assert.Equal("2", result.NewRowVersion);

        // Verify User state
        var user = await context.Users.FirstAsync(u => u.UserId == teacherId);
        Assert.Equal("hashed_new_password_123", user.PasswordHash);
        Assert.Equal(2ul, user.RowVersion);
        Assert.Equal(2u, user.AuthVersion);
        Assert.Equal(now.UtcDateTime, user.UpdatedAt);
        Assert.Equal(_managerId, user.UpdatedBy);

        // Verify Refresh Token revoked
        var token = await context.RefreshTokens.FirstAsync(rt => rt.UserId == teacherId);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal(now.UtcDateTime, token.RevokedAt);
        Assert.Equal("Password reset by CenterManager.", token.RevokeReason);

        // Verify Audit Log
        var audit = await context.AuthorizationAuditLogs
            .FirstOrDefaultAsync(a => a.TargetId == teacherId.ToString("D") && a.ActionType == "TeacherPasswordReset");
        Assert.NotNull(audit);
        Assert.Equal(_centerId, audit.CenterId);
        Assert.Equal(_managerId, audit.ActorUserId);
        Assert.Equal(teacherId, audit.TargetUserId);
        Assert.Equal("trace-teacher-pwd", audit.TraceId);
        Assert.DoesNotContain("SuperSecurePassword123!", audit.BeforeData);
        Assert.DoesNotContain("SuperSecurePassword123!", audit.AfterData);
        Assert.DoesNotContain("hashed_new_password_123", audit.BeforeData);
        Assert.DoesNotContain("hashed_new_password_123", audit.AfterData);
    }

    [Fact]
    public async Task ExecuteAsync_StudentSuccess_UpdatesPassword_BumpsVersions_RevokesTokens_WritesRedactedAudit()
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var studentId = Guid.NewGuid();
        await SeedStudentAsync(context, studentId, rowVersion: 20);

        context.RefreshTokens.Add(new RefreshToken
        {
            CenterId = _centerId,
            UserId = studentId,
            TokenHash = "token_hash_student",
            ExpiresAt = _fixedTime.UtcDateTime.AddDays(7),
            CreatedAt = _fixedTime.UtcDateTime
        });
        await context.SaveChangesAsync();

        var timeProvider = new Mock<TimeProvider>();
        var now = _fixedTime.AddHours(1);
        timeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var useCase = new ResetAccountPasswordUseCase(
            context,
            _mockTenantContext.Object,
            _mockPasswordHasher.Object,
            timeProvider.Object,
            NullLogger<ResetAccountPasswordUseCase>.Instance);

        var request = new ResetAccountPasswordRequest
        {
            NewPassword = "StudentNewPassword123!",
            ExpectedUserRowVersion = "1",
            Reason = "Đặt lại mật khẩu cho học sinh quên mật khẩu"
        };

        var result = await useCase.ExecuteAsync(studentId, nameof(UserRole.Student), request, "trace-student-pwd");

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
        Assert.Equal(studentId.ToString("D"), result.TargetUserId);
        Assert.Equal("2", result.NewRowVersion);

        // Verify User state
        var user = await context.Users.FirstAsync(u => u.UserId == studentId);
        Assert.Equal("hashed_new_password_123", user.PasswordHash);
        Assert.Equal(2ul, user.RowVersion);
        Assert.Equal(2u, user.AuthVersion);
        Assert.Equal(now.UtcDateTime, user.UpdatedAt);
        Assert.Equal(_managerId, user.UpdatedBy);

        // Verify Refresh Token revoked
        var token = await context.RefreshTokens.FirstAsync(rt => rt.UserId == studentId);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal(now.UtcDateTime, token.RevokedAt);

        // Verify Audit Log
        var audit = await context.AuthorizationAuditLogs
            .FirstOrDefaultAsync(a => a.TargetId == studentId.ToString("D") && a.ActionType == "StudentPasswordReset");
        Assert.NotNull(audit);
        Assert.Equal(_centerId, audit.CenterId);
        Assert.Equal(_managerId, audit.ActorUserId);
        Assert.Equal(studentId, audit.TargetUserId);
        Assert.Equal("trace-student-pwd", audit.TraceId);
        Assert.DoesNotContain("StudentNewPassword123!", audit.BeforeData);
        Assert.DoesNotContain("StudentNewPassword123!", audit.AfterData);
    }
}
