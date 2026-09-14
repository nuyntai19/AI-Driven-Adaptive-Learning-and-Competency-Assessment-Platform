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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class DeleteStudentUseCaseTests
{
    private readonly Guid _centerId = Guid.Parse("e331c1f3-18d2-43bb-a5a4-1507dfbb7d90");
    private readonly Guid _managerId = Guid.Parse("84f04c63-4402-4fc9-b6eb-bf89bc5f4923");
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    public DeleteStudentUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_managerId);
        _mockTenantContext.Setup(c => c.Role).Returns("CenterManager");
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

    private async Task SeedDataAsync(
        EduTwinDbContext context,
        Guid studentId,
        bool isDeleted = false,
        CenterStatus centerStatus = CenterStatus.Active,
        bool centerIsDeleted = false,
        UserRole userRole = UserRole.Student,
        bool userIsDeleted = false,
        Guid? studentCenterId = null,
        Guid? userCenterId = null)
    {
        var actualStudentCenterId = studentCenterId ?? _centerId;
        var actualUserCenterId = userCenterId ?? _centerId;

        if (!await context.Centers.AnyAsync(c => c.CenterId == actualStudentCenterId))
        {
            context.Centers.Add(new Center
            {
                CenterId = actualStudentCenterId,
                CenterName = "Test Center",
                CenterCode = "TC",
                Timezone = "Asia/Ho_Chi_Minh",
                Status = centerStatus,
                IsDeleted = centerIsDeleted,
                CreatedAt = _fixedTime.UtcDateTime,
                UpdatedAt = _fixedTime.UtcDateTime
            });
        }

        context.Users.Add(new User
        {
            UserId = studentId,
            CenterId = actualUserCenterId,
            Username = $"student_{studentId:N}",
            PasswordHash = "hash",
            RoleName = userRole,
            DisplayName = "Test Student",
            Status = UserStatus.Active,
            IsDeleted = userIsDeleted,
            AuthVersion = 1,
            RowVersion = 10,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        context.Students.Add(new Student
        {
            StudentId = studentId,
            CenterId = actualStudentCenterId,
            FullName = "Test Student Full Name",
            GradeLevel = 10,
            IsDeleted = isDeleted,
            RowVersion = 10,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(false, true, true, "CenterManager", true)] // Not resolved
    [InlineData(true, false, true, "CenterManager", true)] // No CenterId
    [InlineData(true, true, false, "CenterManager", true)] // No UserId
    [InlineData(true, true, true, "Teacher", true)]        // Wrong role
    [InlineData(true, true, true, "Student", true)]        // Wrong role
    [InlineData(true, true, true, "CenterManager", false)] // Empty studentId
    public async Task ExecuteAsync_InvalidCallerOrContext_ReturnsResourceNotFound(
        bool isResolved, bool hasCenter, bool hasUser, string role, bool hasStudentId)
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var studentId = hasStudentId ? Guid.NewGuid() : Guid.Empty;

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(c => c.IsResolved).Returns(isResolved);
        tenantContext.Setup(c => c.CenterId).Returns(hasCenter ? _centerId : null);
        tenantContext.Setup(c => c.UserId).Returns(hasUser ? _managerId : null);
        tenantContext.Setup(c => c.Role).Returns(role);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new DeleteStudentUseCase(
            context,
            tenantContext.Object,
            timeProvider.Object,
            NullLogger<DeleteStudentUseCase>.Instance);

        var result = await useCase.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterInactiveOrDeleted_ReturnsResourceNotFound()
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var studentId = Guid.NewGuid();
        await SeedDataAsync(context, studentId, centerStatus: CenterStatus.Suspended);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new DeleteStudentUseCase(
            context,
            _mockTenantContext.Object,
            timeProvider.Object,
            NullLogger<DeleteStudentUseCase>.Instance);

        var result = await useCase.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantStudent_ReturnsResourceNotFound()
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var studentId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        await SeedDataAsync(context, studentId, studentCenterId: otherCenterId, userCenterId: otherCenterId);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new DeleteStudentUseCase(
            context,
            _mockTenantContext.Object,
            timeProvider.Object,
            NullLogger<DeleteStudentUseCase>.Instance);

        var result = await useCase.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_NonStudentRole_ReturnsResourceNotFound()
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var studentId = Guid.NewGuid();
        await SeedDataAsync(context, studentId, userRole: UserRole.Teacher);

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new DeleteStudentUseCase(
            context,
            _mockTenantContext.Object,
            timeProvider.Object,
            NullLogger<DeleteStudentUseCase>.Instance);

        var result = await useCase.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_Success_SoftDeletes_RemovesClassMemberships_RevokesTokens_WritesAudit()
    {
        var (context, _) = CreateContext(Guid.NewGuid().ToString());
        var studentId = Guid.NewGuid();
        await SeedDataAsync(context, studentId);

        var classId1 = Guid.NewGuid();
        var classId2 = Guid.NewGuid();
        context.ClassStudents.AddRange(
            new ClassStudent
            {
                CenterId = _centerId,
                ClassId = classId1,
                StudentId = studentId,
                Status = ClassStudentStatus.Active,
                JoinedAt = _fixedTime.UtcDateTime
            },
            new ClassStudent
            {
                CenterId = _centerId,
                ClassId = classId2,
                StudentId = studentId,
                Status = ClassStudentStatus.Active,
                JoinedAt = _fixedTime.UtcDateTime
            }
        );

        context.RefreshTokens.Add(new RefreshToken
        {
            CenterId = _centerId,
            UserId = studentId,
            TokenHash = "token_hash_1",
            ExpiresAt = _fixedTime.UtcDateTime.AddDays(7),
            CreatedAt = _fixedTime.UtcDateTime
        });

        await context.SaveChangesAsync();

        var timeProvider = new Mock<TimeProvider>();
        var now = _fixedTime.AddHours(2);
        timeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        var useCase = new DeleteStudentUseCase(
            context,
            _mockTenantContext.Object,
            timeProvider.Object,
            NullLogger<DeleteStudentUseCase>.Instance);

        var result = await useCase.ExecuteAsync(studentId, "trace-del-123");

        Assert.True(result.IsSuccess);

        // Verify Student soft delete
        var student = await context.Students.IgnoreQueryFilters().FirstAsync(s => s.StudentId == studentId);
        Assert.True(student.IsDeleted);
        Assert.Equal(now.UtcDateTime, student.DeletedAt);
        Assert.Equal(_managerId, student.DeletedBy);

        // Verify User soft delete and session invalidation
        var user = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == studentId);
        Assert.True(user.IsDeleted);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Equal(2u, user.AuthVersion);
        Assert.Equal(2ul, user.RowVersion);

        // Verify Class memberships set to Removed
        var memberships = await context.ClassStudents.Where(cs => cs.StudentId == studentId).ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, cs => Assert.Equal(ClassStudentStatus.Removed, cs.Status));
        Assert.All(memberships, cs => Assert.Equal(now.UtcDateTime, cs.RemovedAt));

        // Verify Refresh token revoked
        var token = await context.RefreshTokens.FirstAsync(rt => rt.UserId == studentId);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal(now.UtcDateTime, token.RevokedAt);
        Assert.Equal("Student account soft-deleted by CenterManager.", token.RevokeReason);

        // Verify Audit log
        var audit = await context.AuthorizationAuditLogs
            .FirstOrDefaultAsync(a => a.TargetId == studentId.ToString("D") && a.ActionType == "StudentDeleted");
        Assert.NotNull(audit);
        Assert.Equal(_centerId, audit.CenterId);
        Assert.Equal(_managerId, audit.ActorUserId);
        Assert.Equal(studentId, audit.TargetUserId);
        Assert.Equal("trace-del-123", audit.TraceId);
        Assert.DoesNotContain("password", audit.AfterData, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", audit.AfterData, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrencyConflict_ReturnsConcurrencyConflict()
    {
        var (context, interceptor) = CreateContext(Guid.NewGuid().ToString());
        var studentId = Guid.NewGuid();
        await SeedDataAsync(context, studentId);

        interceptor.OnSavingChangesAsyncAction = () =>
            throw new DbUpdateConcurrencyException("OCC Conflict");

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var useCase = new DeleteStudentUseCase(
            context,
            _mockTenantContext.Object,
            timeProvider.Object,
            NullLogger<DeleteStudentUseCase>.Instance);

        var result = await useCase.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }
}
