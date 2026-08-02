using System;
using System.Collections.Generic;
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

public class ListAssignmentsUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantMock;

    public ListAssignmentsUseCaseTests()
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

    private ListAssignmentsUseCase CreateSut(EduTwinDbContext ctx) => new(ctx, _tenantMock.Object);

    private void SetupTenant(Guid centerId, Guid userId, string role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

    private async Task SeedAssignmentsAsync(EduTwinDbContext ctx, Guid centerId, Guid teacherId,
        Guid classId, Guid subjectId, int count = 1, AssignmentStatus status = AssignmentStatus.Draft)
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            ctx.Assignments.Add(new Assignment
            {
                AssignmentId = Guid.NewGuid(),
                CenterId = centerId,
                ClassId = classId,
                CreatedByTeacherId = teacherId,
                Title = $"Assignment {i + 1}",
                Status = status,
                IsDeleted = false,
                RowVersion = 1,
                CreatedAt = now.AddMinutes(-i),
                UpdatedAt = now.AddMinutes(-i)
            });
        }
        await ctx.SaveChangesAsync();
    }

    private async Task<(Guid ClassId, Guid SubjectId)> SeedInfraAsync(EduTwinDbContext ctx, Guid centerId, Guid teacherId)
    {
        var now = DateTime.UtcNow;
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

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
        await ctx.SaveChangesAsync();

        return (classId, subjectId);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherSeesOnlyOwnClassAssignments()
    {
        var centerId = Guid.NewGuid();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();
        SetupTenant(centerId, teacher1Id, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (class1Id, _) = await SeedInfraAsync(ctx, centerId, teacher1Id);

        // Seed class and assignments for teacher2
        var now = DateTime.UtcNow;
        var subjectId2 = Guid.NewGuid();
        var class2Id = Guid.NewGuid();
        ctx.Subjects.Add(new Subject
        {
            SubjectId = subjectId2, CenterId = centerId, SubjectCode = "S2", SubjectName = "S2",
            IsActive = true, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Users.Add(new User
        {
            UserId = teacher2Id, CenterId = centerId, Username = "t2", PasswordHash = "h",
            RoleName = UserRole.Teacher, DisplayName = "T2", Status = UserStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Teachers.Add(new Teacher
        {
            TeacherId = teacher2Id, CenterId = centerId, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Classes.Add(new Class
        {
            ClassId = class2Id, CenterId = centerId, TeacherId = teacher2Id, SubjectId = subjectId2,
            ClassName = "Cls2", AcademicYear = "2026", Status = ClassStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        await ctx.SaveChangesAsync();

        // Seed 2 assignments for teacher1's class, 3 for teacher2's class
        await SeedAssignmentsAsync(ctx, centerId, teacher1Id, class1Id, Guid.NewGuid(), 2);
        await SeedAssignmentsAsync(ctx, centerId, teacher2Id, class2Id, Guid.NewGuid(), 3);

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(new ListAssignmentsQuery { Page = 1, PageSize = 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);   // teacher1 sees only own
        Assert.Equal(2, result.TotalItems);
    }

    [Fact]
    public async Task ExecuteAsync_FilterByStatus_ReturnOnlyMatchingStatus()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (classId, _) = await SeedInfraAsync(ctx, centerId, teacherId);

        await SeedAssignmentsAsync(ctx, centerId, teacherId, classId, Guid.NewGuid(), 2, AssignmentStatus.Draft);
        await SeedAssignmentsAsync(ctx, centerId, teacherId, classId, Guid.NewGuid(), 1, AssignmentStatus.Published);

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(new ListAssignmentsQuery { Status = "Draft", Page = 1, PageSize = 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.TotalItems);
        Assert.All(result.Data!, a => Assert.Equal("Draft", a.Status));
    }

    [Fact]
    public async Task ExecuteAsync_FilterByClassId_ReturnOnlyMatchingClass()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (class1Id, subjectId) = await SeedInfraAsync(ctx, centerId, teacherId);

        // Add second class for same teacher
        var now = DateTime.UtcNow;
        var class2Id = Guid.NewGuid();
        ctx.Classes.Add(new Class
        {
            ClassId = class2Id, CenterId = centerId, TeacherId = teacherId, SubjectId = subjectId,
            ClassName = "Cls2", AcademicYear = "2026", Status = ClassStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        await ctx.SaveChangesAsync();

        await SeedAssignmentsAsync(ctx, centerId, teacherId, class1Id, subjectId, 3);
        await SeedAssignmentsAsync(ctx, centerId, teacherId, class2Id, subjectId, 2);

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(new ListAssignmentsQuery { ClassId = class1Id, Page = 1, PageSize = 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.TotalItems);
        Assert.All(result.Data!, a => Assert.Equal(class1Id.ToString().ToLowerInvariant(), a.ClassId));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCenter_ReturnsEmptyList()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        await SeedInfraAsync(ctx, centerId, teacherId);  // no assignments seeded

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(new ListAssignmentsQuery { Page = 1, PageSize = 20 });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidStatus_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new ListAssignmentsQuery { Status = "InvalidStatus" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }
}
