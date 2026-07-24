using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class ListCurriculumsUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly ListCurriculumsUseCase _sut;

    public ListCurriculumsUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
        _sut = new ListCurriculumsUseCase(_dbContext, _mockTenantContext.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SetupTenant(Guid? centerId, Guid? userId, string? role, bool isResolved = true)
    {
        _mockTenantContext.Setup(c => c.IsResolved).Returns(isResolved);
        _mockTenantContext.Setup(c => c.CenterId).Returns(centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(userId);
        _mockTenantContext.Setup(c => c.Role).Returns(role);
    }

    private async Task<(Center Center, User User, Teacher Teacher, Subject Subject)> SeedBasicEntitiesAsync(Guid centerId, Guid teacherId)
    {
        var now = DateTime.UtcNow;

        var center = new Center
        {
            CenterId = centerId,
            CenterName = "Center " + centerId.ToString()[..8],
            CenterCode = "C-" + centerId.ToString()[..4],
            Timezone = "Asia/Ho_Chi_Minh",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var user = new User
        {
            UserId = teacherId,
            CenterId = centerId,
            Username = "teacher-" + teacherId.ToString()[..8],
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            DisplayName = "Teacher One",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var teacher = new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var subject = new Subject
        {
            SubjectId = Guid.NewGuid(),
            CenterId = centerId,
            SubjectCode = "MATH12",
            SubjectName = "Toán 12",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(user);
        _dbContext.Teachers.Add(teacher);
        _dbContext.Subjects.Add(subject);
        await _dbContext.SaveChangesAsync();

        return (center, user, teacher, subject);
    }

    private async Task<User> SeedCenterManagerAsync(Guid centerId, Guid managerId)
    {
        var now = DateTime.UtcNow;
        var managerUser = new User
        {
            UserId = managerId,
            CenterId = centerId,
            Username = "manager-" + managerId.ToString()[..8],
            PasswordHash = "hash",
            RoleName = UserRole.CenterManager,
            DisplayName = "Manager One",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.Add(managerUser);
        await _dbContext.SaveChangesAsync();
        return managerUser;
    }

    // --- A. Success / Visibility ---

    [Fact]
    public async Task ExecuteAsync_TeacherOwner_ReturnsOnlyOwnCurriculums()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var ownCurriculum = new Curriculum
        {
            CurriculumId = Guid.NewGuid(),
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = seed.Subject.SubjectId,
            Title = "Own Curriculum",
            ReviewStatus = ReviewStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        _dbContext.Curriculums.Add(ownCurriculum);
        await _dbContext.SaveChangesAsync();

        var query = new CurriculumListQuery();
        var result = await _sut.ExecuteAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(ownCurriculum.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherCaller_DoesNotSeeOtherTeacherCurriculumsInSameCenter()
    {
        var centerId = Guid.NewGuid();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();
        SetupTenant(centerId, teacher1Id, nameof(UserRole.Teacher));

        var seed1 = await SeedBasicEntitiesAsync(centerId, teacher1Id);

        var now = DateTime.UtcNow;
        var user2 = new User { UserId = teacher2Id, CenterId = centerId, Username = "t2", PasswordHash = "h", RoleName = UserRole.Teacher, DisplayName = "T2", Status = UserStatus.Active, CreatedAt = now, UpdatedAt = now };
        var teacher2 = new Teacher { TeacherId = teacher2Id, CenterId = centerId, CreatedAt = now, UpdatedAt = now };
        _dbContext.Users.Add(user2);
        _dbContext.Teachers.Add(teacher2);

        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacher1Id, SubjectId = seed1.Subject.SubjectId, Title = "C1", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var c2 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacher2Id, SubjectId = seed1.Subject.SubjectId, Title = "C2", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };

        _dbContext.Curriculums.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_ReturnsAllCurriculumsInCenter()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        await SeedBasicEntitiesAsync(centerId, teacher1Id);
        await SeedCenterManagerAsync(centerId, managerId);

        var now = DateTime.UtcNow;
        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacher1Id, SubjectId = Guid.NewGuid(), Title = "C1", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var c2 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacher2Id, SubjectId = Guid.NewGuid(), Title = "C2", ReviewStatus = ReviewStatus.Published, CreatedAt = now, UpdatedAt = now };

        _dbContext.Curriculums.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantCurriculum_IsExcluded()
    {
        var center1Id = Guid.NewGuid();
        var center2Id = Guid.NewGuid();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();

        SetupTenant(center1Id, teacher1Id, nameof(UserRole.Teacher));
        var seed1 = await SeedBasicEntitiesAsync(center1Id, teacher1Id);
        var seed2 = await SeedBasicEntitiesAsync(center2Id, teacher2Id);

        var now = DateTime.UtcNow;
        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = center1Id, TeacherId = teacher1Id, SubjectId = seed1.Subject.SubjectId, Title = "C1", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var c2 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = center2Id, TeacherId = teacher2Id, SubjectId = seed2.Subject.SubjectId, Title = "C2", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };

        _dbContext.Curriculums.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_SoftDeletedCurriculum_IsExcluded()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var cActive = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Active", IsDeleted = false, CreatedAt = now, UpdatedAt = now };
        var cDeleted = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Deleted", IsDeleted = true, CreatedAt = now, UpdatedAt = now };

        _dbContext.Curriculums.AddRange(cActive, cDeleted);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(cActive.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_NoCurriculumsMatch_ReturnsSuccessWithEmptyList()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        await SeedBasicEntitiesAsync(centerId, teacherId);

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    // --- B. Tenant Fail-Closed ---

    [Fact]
    public async Task ExecuteAsync_UnresolvedTenant_ReturnsResourceNotFound()
    {
        SetupTenant(Guid.NewGuid(), Guid.NewGuid(), nameof(UserRole.Teacher), isResolved: false);
        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCenterId_ReturnsResourceNotFound()
    {
        SetupTenant(null, Guid.NewGuid(), nameof(UserRole.Teacher));
        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCenterId_ReturnsResourceNotFound()
    {
        SetupTenant(Guid.Empty, Guid.NewGuid(), nameof(UserRole.Teacher));
        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_MissingUserId_ReturnsResourceNotFound()
    {
        SetupTenant(Guid.NewGuid(), null, nameof(UserRole.Teacher));
        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUserId_ReturnsResourceNotFound()
    {
        SetupTenant(Guid.NewGuid(), Guid.Empty, nameof(UserRole.Teacher));
        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCenter_ReturnsResourceNotFound()
    {
        var missingCenterId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(missingCenterId, teacherId, nameof(UserRole.Teacher));

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_InactiveCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.Center.Status = CenterStatus.Suspended;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_DeletedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.Center.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("teacher")]
    [InlineData("centerManager")]
    [InlineData("0")]
    [InlineData("Student")]
    public async Task ExecuteAsync_InvalidOrWrongCasingRole_ReturnsResourceNotFound(string? role)
    {
        SetupTenant(Guid.NewGuid(), Guid.NewGuid(), role);
        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // --- C. Actor Predicates ---

    [Fact]
    public async Task ExecuteAsync_TeacherProfileMissing_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        _dbContext.Teachers.Remove(seed.Teacher);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherProfileDeleted_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.Teacher.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherLinkedUserDeleted_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.User.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherLinkedUserLocked_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.User.Status = UserStatus.Locked;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherLinkedUserDisabled_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.User.Status = UserStatus.Disabled;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherLinkedUserWrongRole_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.User.RoleName = UserRole.CenterManager;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherLinkedUserMissing_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var now = DateTime.UtcNow;

        // Seed active Center
        var center = new Center
        {
            CenterId = centerId,
            CenterName = "Center-MissingUser",
            CenterCode = "CMU",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Centers.Add(center);

        // Seed Teacher profile at same Center, not deleted — but NO linked User
        var teacher = new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Teachers.Add(teacher);
        await _dbContext.SaveChangesAsync();

        // Verify fixture: Center exists, Teacher exists, no User with this ID
        Assert.NotNull(await _dbContext.Centers.FindAsync(centerId));
        Assert.NotNull(await _dbContext.Teachers.FindAsync(teacherId));
        Assert.Null(await _dbContext.Users.FindAsync(teacherId));

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherLinkedUserCrossTenant_ReturnsResourceNotFound()
    {
        var centerAId = Guid.NewGuid();
        var centerBId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerAId, teacherId, nameof(UserRole.Teacher));

        var now = DateTime.UtcNow;

        // Seed both Centers as active
        var centerA = new Center
        {
            CenterId = centerAId,
            CenterName = "CenterA",
            CenterCode = "CA",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        var centerB = new Center
        {
            CenterId = centerBId,
            CenterName = "CenterB",
            CenterCode = "CB",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Centers.AddRange(centerA, centerB);

        // Teacher profile at Center A
        var teacher = new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerAId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Teachers.Add(teacher);

        // User with same TeacherId but belongs to Center B
        var user = new User
        {
            UserId = teacherId,
            CenterId = centerBId,
            Username = "cross-tenant-teacher",
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            DisplayName = "CrossTenant Teacher",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Verify fixture: both Centers exist, Teacher exists at A, User exists at B
        Assert.NotNull(await _dbContext.Centers.FindAsync(centerAId));
        Assert.NotNull(await _dbContext.Centers.FindAsync(centerBId));
        Assert.NotNull(await _dbContext.Teachers.FindAsync(teacherId));
        var dbUser = await _dbContext.Users.FindAsync(teacherId);
        Assert.NotNull(dbUser);
        Assert.Equal(centerBId, dbUser!.CenterId);

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerUserCrossTenant_ReturnsResourceNotFound()
    {
        var centerAId = Guid.NewGuid();
        var centerBId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        SetupTenant(centerAId, managerId, nameof(UserRole.CenterManager));

        var now = DateTime.UtcNow;

        // Seed Center A (active)
        var centerA = new Center
        {
            CenterId = centerAId,
            CenterName = "CenterA",
            CenterCode = "CA",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        // Seed Center B (active)
        var centerB = new Center
        {
            CenterId = centerBId,
            CenterName = "CenterB",
            CenterCode = "CB",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Centers.AddRange(centerA, centerB);

        // Manager User with correct ID but belongs to Center B
        var managerUser = new User
        {
            UserId = managerId,
            CenterId = centerBId,
            Username = "cross-tenant-manager",
            PasswordHash = "hash",
            RoleName = UserRole.CenterManager,
            DisplayName = "CrossTenant Manager",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Users.Add(managerUser);
        await _dbContext.SaveChangesAsync();

        // Verify fixture: Center A exists, Manager User exists at Center B
        Assert.NotNull(await _dbContext.Centers.FindAsync(centerAId));
        var dbUser = await _dbContext.Users.FindAsync(managerId);
        Assert.NotNull(dbUser);
        Assert.Equal(centerBId, dbUser!.CenterId);
        Assert.Equal(UserRole.CenterManager, dbUser.RoleName);
        Assert.Equal(UserStatus.Active, dbUser.Status);
        Assert.False(dbUser.IsDeleted);

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantMappings_AreExcludedFromProjection()
    {
        var centerAId = Guid.NewGuid();
        var centerBId = Guid.NewGuid();
        var teacherAId = Guid.NewGuid();
        var teacherBId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        // Use CenterManager at Center A to see all Center A curriculums
        SetupTenant(centerAId, managerId, nameof(UserRole.CenterManager));

        // Seed full valid fixtures at both Centers
        var seedA = await SeedBasicEntitiesAsync(centerAId, teacherAId);
        var seedB = await SeedBasicEntitiesAsync(centerBId, teacherBId);
        await SeedCenterManagerAsync(centerAId, managerId);

        var now = DateTime.UtcNow;
        var curriculumIdA = Guid.NewGuid();
        var curriculumIdB = Guid.NewGuid();
        var classIdA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var classIdB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Curriculum at Center A
        var curriculumA = new Curriculum
        {
            CurriculumId = curriculumIdA,
            CenterId = centerAId,
            TeacherId = teacherAId,
            SubjectId = seedA.Subject.SubjectId,
            Title = "CenterA-Curriculum",
            ReviewStatus = ReviewStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Curriculum at Center B (adversarial)
        var curriculumB = new Curriculum
        {
            CurriculumId = curriculumIdB,
            CenterId = centerBId,
            TeacherId = teacherBId,
            SubjectId = seedB.Subject.SubjectId,
            Title = "CenterB-Curriculum",
            ReviewStatus = ReviewStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Curriculums.AddRange(curriculumA, curriculumB);

        // Seed Class entities referenced by CurriculumClass FK
        var classA = new Class { ClassId = classIdA, CenterId = centerAId, TeacherId = teacherAId, SubjectId = seedA.Subject.SubjectId, ClassName = "ClassA", AcademicYear = "2026", Status = ClassStatus.Active, CreatedAt = now, UpdatedAt = now };
        var classB = new Class { ClassId = classIdB, CenterId = centerBId, TeacherId = teacherBId, SubjectId = seedB.Subject.SubjectId, ClassName = "ClassB", AcademicYear = "2026", Status = ClassStatus.Active, CreatedAt = now, UpdatedAt = now };
        _dbContext.Classes.AddRange(classA, classB);

        // Seed KnowledgeNode entities referenced by CurriculumNode FK
        var nodeA = new KnowledgeNode { NodeId = 100, CenterId = centerAId, SubjectId = seedA.Subject.SubjectId, NodeType = NodeType.Topic, NodeCode = "NA", NodeName = "NodeA", IsActive = true, CreatedAt = now, UpdatedAt = now };
        var nodeB = new KnowledgeNode { NodeId = 999, CenterId = centerBId, SubjectId = seedB.Subject.SubjectId, NodeType = NodeType.Topic, NodeCode = "NB", NodeName = "NodeB", IsActive = true, CreatedAt = now, UpdatedAt = now };
        _dbContext.KnowledgeNodes.AddRange(nodeA, nodeB);
        await _dbContext.SaveChangesAsync();

        // Valid mapping at Center A
        var ccA = new CurriculumClass { CenterId = centerAId, CurriculumId = curriculumIdA, ClassId = classIdA, AssignedAt = now, AssignedBy = teacherAId };
        var cnA = new CurriculumNode { CenterId = centerAId, CurriculumId = curriculumIdA, NodeId = 100, OrderIndex = 1, CreatedAt = now };

        // Adversarial mapping at Center B (different CurriculumId, different Center)
        var ccB = new CurriculumClass { CenterId = centerBId, CurriculumId = curriculumIdB, ClassId = classIdB, AssignedAt = now, AssignedBy = teacherBId };
        var cnB = new CurriculumNode { CenterId = centerBId, CurriculumId = curriculumIdB, NodeId = 999, OrderIndex = 1, CreatedAt = now };

        _dbContext.CurriculumClasses.AddRange(ccA, ccB);
        _dbContext.CurriculumNodes.AddRange(cnA, cnB);
        await _dbContext.SaveChangesAsync();

        // Verify both tenant fixtures exist before executing the use case.
        Assert.NotNull(await _dbContext.Centers.FindAsync(centerBId));
        Assert.NotNull(await _dbContext.Curriculums.FindAsync(curriculumIdB));
        var allClasses = await _dbContext.CurriculumClasses
            .IgnoreQueryFilters()
            .ToListAsync();
        Assert.Contains(allClasses, cc =>
            cc.CenterId == centerAId &&
            cc.CurriculumId == curriculumIdA &&
            cc.ClassId == classIdA);
        Assert.Contains(allClasses, cc =>
            cc.CenterId == centerBId &&
            cc.CurriculumId == curriculumIdB &&
            cc.ClassId == classIdB);

        var allNodes = await _dbContext.CurriculumNodes
            .IgnoreQueryFilters()
            .ToListAsync();
        Assert.Contains(allNodes, cn =>
            cn.CenterId == centerAId &&
            cn.CurriculumId == curriculumIdA &&
            cn.NodeId == nodeA.NodeId);
        Assert.Contains(allNodes, cn =>
            cn.CenterId == centerBId &&
            cn.CurriculumId == curriculumIdB &&
            cn.NodeId == nodeB.NodeId);

        _dbContext.ChangeTracker.Clear();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        // CenterManager at Center A should see only curriculumA
        Assert.Single(result.Data);
        var dto = result.Data[0];
        Assert.Equal(curriculumIdA.ToString("D").ToLowerInvariant(), dto.CurriculumId);

        // ClassIds must contain only Center A mapping
        Assert.Single(dto.ClassIds);
        Assert.Equal(classIdA.ToString("D").ToLowerInvariant(), dto.ClassIds[0]);
        Assert.DoesNotContain(classIdB.ToString("D").ToLowerInvariant(), dto.ClassIds);

        // NodeIds must contain only Center A mapping
        Assert.Single(dto.NodeIds);
        Assert.Equal(
            nodeA.NodeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            dto.NodeIds[0]);
        Assert.DoesNotContain(
            nodeB.NodeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            dto.NodeIds);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerUserMissing_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));
        await SeedBasicEntitiesAsync(centerId, Guid.NewGuid());

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerUserDeleted_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));
        await SeedBasicEntitiesAsync(centerId, Guid.NewGuid());
        var manager = await SeedCenterManagerAsync(centerId, managerId);
        manager.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerUserLocked_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));
        await SeedBasicEntitiesAsync(centerId, Guid.NewGuid());
        var manager = await SeedCenterManagerAsync(centerId, managerId);
        manager.Status = UserStatus.Locked;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerUserDisabled_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));
        await SeedBasicEntitiesAsync(centerId, Guid.NewGuid());
        var manager = await SeedCenterManagerAsync(centerId, managerId);
        manager.Status = UserStatus.Disabled;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerUserWrongRole_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));
        await SeedBasicEntitiesAsync(centerId, Guid.NewGuid());
        var manager = await SeedCenterManagerAsync(centerId, managerId);
        manager.RoleName = UserRole.Teacher;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // --- D. Query Validation ---

    [Fact]
    public async Task ExecuteAsync_EmptySubjectId_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        await SeedBasicEntitiesAsync(centerId, teacherId);

        var query = new CurriculumListQuery { SubjectId = Guid.Empty };
        var result = await _sut.ExecuteAsync(query);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Published")]
    [InlineData("Archived")]
    public async Task ExecuteAsync_ValidStatus_DraftPublishedArchived_Passes(string status)
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        await SeedBasicEntitiesAsync(centerId, teacherId);

        var query = new CurriculumListQuery { Status = status };
        var result = await _sut.ExecuteAsync(query);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_NullStatus_DoesNotFilterByStatus()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "C1", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var c2 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "C2", ReviewStatus = ReviewStatus.Published, CreatedAt = now, UpdatedAt = now };
        _dbContext.Curriculums.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var query = new CurriculumListQuery { Status = null };
        var result = await _sut.ExecuteAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("draft")]
    [InlineData("published")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("Admin")]
    public async Task ExecuteAsync_InvalidStatus_ReturnsValidationFailed(string invalidStatus)
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        await SeedBasicEntitiesAsync(centerId, teacherId);

        var query = new CurriculumListQuery { Status = invalidStatus };
        var result = await _sut.ExecuteAsync(query);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    // --- E. Filtering ---

    [Fact]
    public async Task ExecuteAsync_SubjectIdFilter_ReturnsOnlyMatchingSubject()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var subject2Id = Guid.NewGuid();

        var now = DateTime.UtcNow;
        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "S1", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var c2 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = subject2Id, Title = "S2", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        _dbContext.Curriculums.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var query = new CurriculumListQuery { SubjectId = seed.Subject.SubjectId };
        var result = await _sut.ExecuteAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_StatusFilter_ReturnsOnlyMatchingStatus()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Draft", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var c2 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Published", ReviewStatus = ReviewStatus.Published, CreatedAt = now, UpdatedAt = now };
        _dbContext.Curriculums.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var query = new CurriculumListQuery { Status = "Published" };
        var result = await _sut.ExecuteAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c2.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_SubjectIdAndStatusFilter_CombinedCorrectly()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var sub2Id = Guid.NewGuid();

        var now = DateTime.UtcNow;
        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Match", ReviewStatus = ReviewStatus.Published, CreatedAt = now, UpdatedAt = now };
        var c2 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "NoMatchStatus", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var c3 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = sub2Id, Title = "NoMatchSubject", ReviewStatus = ReviewStatus.Published, CreatedAt = now, UpdatedAt = now };
        _dbContext.Curriculums.AddRange(c1, c2, c3);
        await _dbContext.SaveChangesAsync();

        var query = new CurriculumListQuery { SubjectId = seed.Subject.SubjectId, Status = "Published" };
        var result = await _sut.ExecuteAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_FilterNoMatch_ReturnsSuccessWithEmptyList()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Draft", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        _dbContext.Curriculums.Add(c1);
        await _dbContext.SaveChangesAsync();

        var query = new CurriculumListQuery { Status = "Archived" };
        var result = await _sut.ExecuteAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_FilterDoesNotExposeCrossTenantRecords()
    {
        var center1Id = Guid.NewGuid();
        var center2Id = Guid.NewGuid();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();

        SetupTenant(center1Id, teacher1Id, nameof(UserRole.Teacher));
        var seed1 = await SeedBasicEntitiesAsync(center1Id, teacher1Id);
        var seed2 = await SeedBasicEntitiesAsync(center2Id, teacher2Id);

        var now = DateTime.UtcNow;
        var c1 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = center1Id, TeacherId = teacher1Id, SubjectId = seed1.Subject.SubjectId, Title = "Center1", ReviewStatus = ReviewStatus.Published, CreatedAt = now, UpdatedAt = now };
        var c2 = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = center2Id, TeacherId = teacher2Id, SubjectId = seed2.Subject.SubjectId, Title = "Center2", ReviewStatus = ReviewStatus.Published, CreatedAt = now, UpdatedAt = now };
        _dbContext.Curriculums.AddRange(c1, c2);
        await _dbContext.SaveChangesAsync();

        var query = new CurriculumListQuery { Status = "Published" };
        var result = await _sut.ExecuteAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
    }

    // --- F. Ordering / Projection ---

    [Fact]
    public async Task ExecuteAsync_OrdersByUpdatedAtDescending()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var cOld = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Old", ReviewStatus = ReviewStatus.Draft, CreatedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-2) };
        var cNew = new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "New", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        _dbContext.Curriculums.AddRange(cOld, cNew);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(cNew.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
        Assert.Equal(cOld.CurriculumId.ToString("D").ToLowerInvariant(), result.Data[1].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_OrdersByCurriculumIdAscending_WhenUpdatedAtEqual()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var fixedTime = DateTime.UtcNow;
        var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var c2 = new Curriculum { CurriculumId = id2, CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "C2", ReviewStatus = ReviewStatus.Draft, CreatedAt = fixedTime, UpdatedAt = fixedTime };
        var c1 = new Curriculum { CurriculumId = id1, CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "C1", ReviewStatus = ReviewStatus.Draft, CreatedAt = fixedTime, UpdatedAt = fixedTime };
        _dbContext.Curriculums.AddRange(c2, c1);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(id1.ToString("D").ToLowerInvariant(), result.Data[0].CurriculumId);
        Assert.Equal(id2.ToString("D").ToLowerInvariant(), result.Data[1].CurriculumId);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectsCanonicalLowercaseGuidFields()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var c = new Curriculum
        {
            CurriculumId = Guid.NewGuid(),
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = seed.Subject.SubjectId,
            Title = "Title",
            ReviewStatus = ReviewStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Curriculums.Add(c);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var dto = result.Data[0];
        Assert.Equal(c.CurriculumId.ToString("D").ToLowerInvariant(), dto.CurriculumId);
        Assert.Equal(teacherId.ToString("D").ToLowerInvariant(), dto.TeacherId);
        Assert.Equal(seed.Subject.SubjectId.ToString("D").ToLowerInvariant(), dto.SubjectId);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectsRowVersionAsInvariantString()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var c = new Curriculum
        {
            CurriculumId = Guid.NewGuid(),
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = seed.Subject.SubjectId,
            Title = "Title",
            ReviewStatus = ReviewStatus.Draft,
            RowVersion = 42,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Curriculums.Add(c);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(c.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), result.Data[0].RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectsClassIdsOrderedByClassIdAscending()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var cId = Guid.NewGuid();
        var classId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var classId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var now = DateTime.UtcNow;
        var c = new Curriculum { CurriculumId = cId, CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Classes", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var cc2 = new CurriculumClass { CenterId = centerId, CurriculumId = cId, ClassId = classId2, AssignedAt = now, AssignedBy = teacherId };
        var cc1 = new CurriculumClass { CenterId = centerId, CurriculumId = cId, ClassId = classId1, AssignedAt = now, AssignedBy = teacherId };

        _dbContext.Curriculums.Add(c);
        _dbContext.CurriculumClasses.AddRange(cc2, cc1);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var classIds = result.Data[0].ClassIds;
        Assert.Equal(2, classIds.Count);
        Assert.Equal(classId1.ToString("D").ToLowerInvariant(), classIds[0]);
        Assert.Equal(classId2.ToString("D").ToLowerInvariant(), classIds[1]);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectsNodeIdsOrderedByOrderIndexAscending()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var cId = Guid.NewGuid();

        var now = DateTime.UtcNow;
        var c = new Curriculum { CurriculumId = cId, CenterId = centerId, TeacherId = teacherId, SubjectId = seed.Subject.SubjectId, Title = "Nodes", ReviewStatus = ReviewStatus.Draft, CreatedAt = now, UpdatedAt = now };
        var cn2 = new CurriculumNode { CenterId = centerId, CurriculumId = cId, NodeId = 200, OrderIndex = 2, CreatedAt = now };
        var cn1 = new CurriculumNode { CenterId = centerId, CurriculumId = cId, NodeId = 100, OrderIndex = 1, CreatedAt = now };

        _dbContext.Curriculums.Add(c);
        _dbContext.CurriculumNodes.AddRange(cn2, cn1);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var nodeIds = result.Data[0].NodeIds;
        Assert.Equal(2, nodeIds.Count);
        Assert.Equal("100", nodeIds[0]);
        Assert.Equal("200", nodeIds[1]);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectsDescriptionSourceFileAndReviewStatusCorrectly()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var c = new Curriculum
        {
            CurriculumId = Guid.NewGuid(),
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = seed.Subject.SubjectId,
            Title = "Full Specs",
            Description = "A detailed description",
            SourceFile = "curriculum-v1.pdf",
            ReviewStatus = ReviewStatus.Published,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Curriculums.Add(c);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var dto = result.Data[0];
        Assert.Equal("Full Specs", dto.Title);
        Assert.Equal("A detailed description", dto.Description);
        Assert.Equal("curriculum-v1.pdf", dto.SourceFile);
        Assert.Equal("Published", dto.ReviewStatus);
    }

    [Fact]
    public async Task ExecuteAsync_ReadPathDoesNotLeaveEntitiesTracked()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var now = DateTime.UtcNow;
        var cId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var c = new Curriculum
        {
            CurriculumId = cId,
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = seed.Subject.SubjectId,
            Title = "NoTracking",
            ReviewStatus = ReviewStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        var classEntity = new Class
        {
            ClassId = classId,
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = seed.Subject.SubjectId,
            ClassName = "NoTracking Class",
            AcademicYear = "2026",
            Status = ClassStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        var node = new KnowledgeNode
        {
            NodeId = 42,
            CenterId = centerId,
            SubjectId = seed.Subject.SubjectId,
            NodeType = NodeType.Topic,
            NodeCode = "NO-TRACK",
            NodeName = "No Tracking Node",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Curriculums.Add(c);
        _dbContext.Classes.Add(classEntity);
        _dbContext.KnowledgeNodes.Add(node);
        await _dbContext.SaveChangesAsync();

        var cc = new CurriculumClass
        {
            CenterId = centerId,
            CurriculumId = cId,
            ClassId = classId,
            AssignedAt = now,
            AssignedBy = teacherId
        };
        var cn = new CurriculumNode
        {
            CenterId = centerId,
            CurriculumId = cId,
            NodeId = node.NodeId,
            OrderIndex = 1,
            CreatedAt = now
        };
        _dbContext.CurriculumClasses.Add(cc);
        _dbContext.CurriculumNodes.Add(cn);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var result = await _sut.ExecuteAsync(new CurriculumListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Empty(_dbContext.ChangeTracker.Entries<Curriculum>());
        Assert.Empty(_dbContext.ChangeTracker.Entries<CurriculumClass>());
        Assert.Empty(_dbContext.ChangeTracker.Entries<CurriculumNode>());
    }

    // --- G. Cancellation / Security ---

    [Fact]
    public async Task ExecuteAsync_HonorsCancellationToken()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        await SeedBasicEntitiesAsync(centerId, teacherId);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ExecuteAsync(new CurriculumListQuery(), cts.Token));
    }

    [Fact]
    public void ProductionSourceFile_Exists_AndDoesNotUseIgnoreQueryFilters()
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "EduTwin.BLL", "CurriculumAndQuestions", "ListCurriculumsUseCase.cs");
        var fullPath = Path.GetFullPath(filePath);

        Assert.True(File.Exists(fullPath), $"Production file not found at path: {fullPath}");

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain("IgnoreQueryFilters", content);
    }

    [Fact]
    public void CurriculumAndQuestionsDependencyInjection_ResolvesListCurriculumsUseCase()
    {
        var services = new ServiceCollection();
        services.AddCurriculumAndQuestions();

        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IListCurriculumsUseCase));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(ListCurriculumsUseCase), descriptor.ImplementationType);
    }
}
