using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Moq;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.BLL.IdentityAndTenancy;

namespace EduTwin.BLL.Tests.Assignments;

public class ListStudentAssignmentsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_TeacherAccountType_FailsClosed()
    {
        var centerId = Guid.NewGuid();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(item => item.IsResolved).Returns(true);
        tenantContext.SetupGet(item => item.CenterId).Returns(centerId);
        tenantContext.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        tenantContext.SetupGet(item => item.Role).Returns("Teacher");
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(item => item.CenterId).Returns(centerId);
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new EduTwinDbContext(options, accessor.Object);
        var sut = new ListStudentAssignmentsUseCase(
            context,
            tenantContext.Object,
            TimeProvider.System);

        var result = await sut.ExecuteAsync(
            new ListStudentAssignmentsQuery(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAssignments_WhenAuthorized()
    {
        // Arrange
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
        tenantContextMock.Setup(x => x.UserId).Returns(studentId);
        tenantContextMock.Setup(x => x.Role).Returns("Student");
        tenantContextMock.Setup(x => x.IsResolved).Returns(true);

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);

        using var context = new EduTwinDbContext(options, tenantAccessorMock.Object);

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            CenterId = centerId,
            Title = "Test Assignment",
            Status = AssignmentStatus.Published,
            DueAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var progress = new StudentAssignmentProgress
        {
            ProgressId = 1,
            CenterId = centerId,
            StudentId = studentId,
            AssignmentId = assignment.AssignmentId,
            Status = ProgressStatus.InProgress,
            CompletedQuestionCount = 0,
            TotalQuestionCount = 5,
            Assignment = assignment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Assignments.Add(assignment);
        context.StudentAssignmentProgresses.Add(progress);
        await context.SaveChangesAsync();

        var useCase = new ListStudentAssignmentsUseCase(context, tenantContextMock.Object, TimeProvider.System);
        var query = new ListStudentAssignmentsQuery();

        // Act
        var result = await useCase.ExecuteAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Data);
        Assert.Equal("InProgress", result.Data.Data[0].Progress.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFilterOverdueAssignments_Correctly()
    {
        // Arrange
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
        tenantContextMock.Setup(x => x.UserId).Returns(studentId);
        tenantContextMock.Setup(x => x.Role).Returns("Student");
        tenantContextMock.Setup(x => x.IsResolved).Returns(true);

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);

        using var context = new EduTwinDbContext(options, tenantAccessorMock.Object);

        var assignment1 = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            CenterId = centerId,
            Title = "Overdue Assignment",
            Status = AssignmentStatus.Published,
            DueAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var assignment2 = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            CenterId = centerId,
            Title = "Active Assignment",
            Status = AssignmentStatus.Published,
            DueAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var progress1 = new StudentAssignmentProgress
        {
            ProgressId = 1,
            CenterId = centerId,
            StudentId = studentId,
            AssignmentId = assignment1.AssignmentId,
            Status = ProgressStatus.InProgress, // Will be projected to Overdue
            CompletedQuestionCount = 0,
            TotalQuestionCount = 5,
            Assignment = assignment1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var progress2 = new StudentAssignmentProgress
        {
            ProgressId = 2,
            CenterId = centerId,
            StudentId = studentId,
            AssignmentId = assignment2.AssignmentId,
            Status = ProgressStatus.InProgress,
            CompletedQuestionCount = 0,
            TotalQuestionCount = 5,
            Assignment = assignment2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Assignments.AddRange(assignment1, assignment2);
        context.StudentAssignmentProgresses.AddRange(progress1, progress2);
        await context.SaveChangesAsync();

        var useCase = new ListStudentAssignmentsUseCase(context, tenantContextMock.Object, TimeProvider.System);
        var query = new ListStudentAssignmentsQuery { Status = "Overdue" };

        // Act
        var result = await useCase.ExecuteAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Data);
        Assert.Equal("Overdue Assignment", result.Data.Data[0].Title);
        Assert.Equal("Overdue", result.Data.Data[0].Progress.Status);
    }
}
