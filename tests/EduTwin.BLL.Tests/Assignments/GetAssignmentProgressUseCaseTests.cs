using System;
using System.Threading.Tasks;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Assignments;

public class GetAssignmentProgressUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<TimeProvider> _timeProviderMock = new();
    private readonly DateTimeOffset _utcNow = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    public GetAssignmentProgressUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _timeProviderMock.Setup(provider => provider.GetUtcNow()).Returns(_utcNow);
    }

    private EduTwinDbContext CreateContext(Guid centerId)
    {
        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(accessor => accessor.CenterId).Returns(centerId);
        return new EduTwinDbContext(_options, tenantAccessorMock.Object);
    }

    private GetAssignmentProgressUseCase CreateSut(EduTwinDbContext context) =>
        new(context, _tenantContextMock.Object, _timeProviderMock.Object);

    private void SetupTenant(Guid centerId, Guid userId, UserRole role)
    {
        _tenantContextMock.SetupGet(context => context.IsResolved).Returns(true);
        _tenantContextMock.SetupGet(context => context.CenterId).Returns(centerId);
        _tenantContextMock.SetupGet(context => context.UserId).Returns(userId);
        _tenantContextMock.SetupGet(context => context.Role).Returns(role.ToString());
    }

    private async Task<Guid> SeedAssignmentAsync(
        EduTwinDbContext context,
        Guid centerId,
        Guid teacherId,
        bool includeProgress = true)
    {
        var now = _utcNow.UtcDateTime;
        var classId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        context.Classes.Add(new Class
        {
            ClassId = classId,
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = Guid.NewGuid(),
            ClassName = "Lớp 12A",
            AcademicYear = "2026",
            Status = ClassStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Bài tập",
            Status = AssignmentStatus.Published,
            DueAt = now.AddDays(-1),
            CreatedAt = now,
            UpdatedAt = now
        });

        if (includeProgress)
        {
            var studentAId = Guid.NewGuid();
            var studentBId = Guid.NewGuid();
            context.Students.AddRange(
                new Student
                {
                    StudentId = studentAId,
                    CenterId = centerId,
                    FullName = "An Nguyễn",
                    GradeLevel = 12,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Student
                {
                    StudentId = studentBId,
                    CenterId = centerId,
                    FullName = "Bình Trần",
                    GradeLevel = 12,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            context.StudentAssignmentProgresses.AddRange(
                new StudentAssignmentProgress
                {
                    ProgressId = 1,
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    StudentId = studentAId,
                    Status = ProgressStatus.InProgress,
                    CompletedQuestionCount = 1,
                    TotalQuestionCount = 3,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new StudentAssignmentProgress
                {
                    ProgressId = 2,
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    StudentId = studentBId,
                    Status = ProgressStatus.Completed,
                    CompletedQuestionCount = 3,
                    TotalQuestionCount = 3,
                    CreatedAt = now,
                    UpdatedAt = now
                });
        }

        await context.SaveChangesAsync();
        return assignmentId;
    }

    [Fact]
    public async Task ExecuteAsync_TeacherOwner_ReturnsStudentNamesCountsAndEffectiveStatus()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, UserRole.Teacher);
        await using var context = CreateContext(centerId);
        var assignmentId = await SeedAssignmentAsync(context, centerId, teacherId);

        var result = await CreateSut(context).ExecuteAsync(assignmentId);

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal(2, result.Data!.Count);
        Assert.Collection(
            result.Data,
            first =>
            {
                Assert.Equal("An Nguyễn", first.FullName);
                Assert.Equal("Overdue", first.Status);
                Assert.Equal(1, first.CompletedQuestionCount);
                Assert.Equal(3, first.TotalQuestionCount);
            },
            second =>
            {
                Assert.Equal("Bình Trần", second.FullName);
                Assert.Equal("Completed", second.Status);
            });
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_ReturnsProgressForAssignmentInCenter()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, Guid.NewGuid(), UserRole.CenterManager);
        await using var context = CreateContext(centerId);
        var assignmentId = await SeedAssignmentAsync(context, centerId, teacherId);

        var result = await CreateSut(context).ExecuteAsync(assignmentId);

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherWhoDoesNotOwnClass_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupTenant(centerId, Guid.NewGuid(), UserRole.Teacher);
        await using var context = CreateContext(centerId);
        var assignmentId = await SeedAssignmentAsync(context, centerId, Guid.NewGuid());

        var result = await CreateSut(context).ExecuteAsync(assignmentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_PublishedAssignmentWithoutProgress_ReturnsEmptyCollection()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, UserRole.Teacher);
        await using var context = CreateContext(centerId);
        var assignmentId = await SeedAssignmentAsync(context, centerId, teacherId, includeProgress: false);

        var result = await CreateSut(context).ExecuteAsync(assignmentId);

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedTenant_ReturnsNotFoundWithoutQueryingData()
    {
        _tenantContextMock.SetupGet(context => context.IsResolved).Returns(false);
        await using var context = CreateContext(Guid.NewGuid());

        var result = await CreateSut(context).ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerCannotReadAssignmentFromAnotherTenant()
    {
        var currentCenterId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        SetupTenant(currentCenterId, Guid.NewGuid(), UserRole.CenterManager);
        await using var context = CreateContext(currentCenterId);
        var assignmentId = await SeedAssignmentAsync(context, otherCenterId, Guid.NewGuid());

        var result = await CreateSut(context).ExecuteAsync(assignmentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }
}
