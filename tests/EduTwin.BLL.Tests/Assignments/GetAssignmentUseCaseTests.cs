using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Assignments;

public class GetAssignmentUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantMock;

    public GetAssignmentUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantMock = new Mock<ITenantContext>();
    }

    private EduTwinDbContext CreateContext(Guid centerId)
    {
        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);
        return new EduTwinDbContext(_options, tenantAccessorMock.Object);
    }

    private GetAssignmentUseCase CreateSut(EduTwinDbContext ctx) => new(ctx, _tenantMock.Object);

    private void SetupTenant(Guid centerId, Guid userId, string role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

    private async Task<(Guid ClassId, Guid AssignmentId)> SeedAsync(EduTwinDbContext ctx, Guid centerId, Guid teacherId)
    {
        var now = DateTime.UtcNow;
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        ctx.Centers.Add(new Center
        {
            CenterId = centerId, CenterCode = "C", CenterName = "C", Status = CenterStatus.Active,
            Timezone = "UTC", IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Subjects.Add(new Subject
        {
            SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S",
            IsActive = true, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Users.Add(new User
        {
            UserId = teacherId, CenterId = centerId, Username = "t", PasswordHash = "h",
            RoleName = UserRole.Teacher, DisplayName = "T", Status = UserStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Teachers.Add(new Teacher
        {
            TeacherId = teacherId, CenterId = centerId, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Classes.Add(new Class
        {
            ClassId = classId, CenterId = centerId, TeacherId = teacherId, SubjectId = subjectId,
            ClassName = "Cls", AcademicYear = "2026", Status = ClassStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId, CenterId = centerId, ClassId = classId,
            CreatedByTeacherId = teacherId, Title = "Test Assignment",
            Status = AssignmentStatus.Draft, IsDeleted = false,
            RowVersion = 1, CreatedAt = now, UpdatedAt = now
        });
        await ctx.SaveChangesAsync();

        return (classId, assignmentId);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherOwner_ReturnsAssignment()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, assignmentId) = await SeedAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(assignmentId);

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.NotNull(result.Data);
        Assert.Equal("Test Assignment", result.Data!.Title);
        Assert.Equal("Draft", result.Data.Status);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherAccessingOtherTeacherAssignment_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();
        // Teacher2 tries to access Teacher1's assignment
        SetupTenant(centerId, teacher2Id, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, assignmentId) = await SeedAsync(ctx, centerId, teacher1Id);

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(assignmentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_CanGetAnyAssignmentInCenter()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        // CenterManager (different user) accesses the assignment
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var ctx = CreateContext(centerId);
        var (_, assignmentId) = await SeedAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(assignmentId);

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_AssignmentNotFound_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        await SeedAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(Guid.NewGuid());  // non-existent id

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedTenant_ReturnsNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);

        var ctx = CreateContext(Guid.NewGuid());
        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }
}
