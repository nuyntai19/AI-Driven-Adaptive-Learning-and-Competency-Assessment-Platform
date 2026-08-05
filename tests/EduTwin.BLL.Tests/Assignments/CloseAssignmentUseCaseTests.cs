using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Assignments;

public class CloseAssignmentUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly DateTimeOffset _fixedUtcNow;

    public CloseAssignmentUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantMock = new Mock<ITenantContext>();

        _fixedUtcNow = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(_fixedUtcNow);
    }

    private EduTwinDbContext CreateContext(Guid centerId)
    {
        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);
        return new EduTwinDbContext(_options, tenantAccessorMock.Object);
    }

    private CloseAssignmentUseCase CreateSut(EduTwinDbContext dbContext) =>
        new(dbContext, _tenantMock.Object, _timeProviderMock.Object);

    private void SetupTenant(Guid centerId, Guid userId, string role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

    private async Task<(Guid CenterId, Guid TeacherId, Guid ClassId, Guid AssignmentId)> SeedDataAsync(AssignmentStatus status, ulong rowVersion = 1)
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var ctx = CreateContext(centerId);

        ctx.Classes.Add(new Class
        {
            ClassId = classId,
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = Guid.NewGuid(),
            ClassName = "Class 1",
            AcademicYear = "2026",
            Status = EduTwin.Contracts.Organization.ClassStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });

        ctx.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Assignment 1",
            Status = status,
            RowVersion = rowVersion,
            CreatedAt = now,
            UpdatedAt = now
        });

        await ctx.SaveChangesAsync();

        return (centerId, teacherId, classId, assignmentId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldChangeStatusToClosed_WhenValidPublishedAssignment()
    {
        // Arrange
        var (centerId, teacherId, _, assignmentId) = await SeedDataAsync(AssignmentStatus.Published, 1);
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        using var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);
        var request = new CloseAssignmentRequest { RowVersion = "1" };

        // Act
        var result = await sut.ExecuteAsync(assignmentId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(AssignmentStatus.Closed.ToString(), result.Data!.Status);
        
        var dbAssignment = await ctx.Assignments.FindAsync(assignmentId);
        Assert.Equal(AssignmentStatus.Closed, dbAssignment!.Status);
        Assert.Equal(2ul, dbAssignment.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnInvalidStateTransition_WhenAssignmentIsDraft()
    {
        // Arrange
        var (centerId, teacherId, _, assignmentId) = await SeedDataAsync(AssignmentStatus.Draft, 1);
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        using var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);
        var request = new CloseAssignmentRequest { RowVersion = "1" };

        // Act
        var result = await sut.ExecuteAsync(assignmentId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnInvalidStateTransition_WhenAssignmentIsAlreadyClosed()
    {
        // Arrange
        var (centerId, teacherId, _, assignmentId) = await SeedDataAsync(AssignmentStatus.Closed, 1);
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        using var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);
        var request = new CloseAssignmentRequest { RowVersion = "1" };

        // Act
        var result = await sut.ExecuteAsync(assignmentId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConcurrencyConflict_WhenRowVersionMismatches()
    {
        // Arrange
        var (centerId, teacherId, _, assignmentId) = await SeedDataAsync(AssignmentStatus.Published, 1);
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        using var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);
        var request = new CloseAssignmentRequest { RowVersion = "999" }; // Mismatch

        // Act
        var result = await sut.ExecuteAsync(assignmentId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnResourceNotFound_WhenTeacherDoesNotOwnClass()
    {
        // Arrange
        var (centerId, _, _, assignmentId) = await SeedDataAsync(AssignmentStatus.Published, 1);
        var otherTeacherId = Guid.NewGuid(); // Different teacher
        SetupTenant(centerId, otherTeacherId, nameof(UserRole.Teacher));

        using var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);
        var request = new CloseAssignmentRequest { RowVersion = "1" };

        // Act
        var result = await sut.ExecuteAsync(assignmentId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }
}
