using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.Organization;

public class ListTeachersUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly ListTeachersUseCase _sut;
    private readonly Guid _centerId = Guid.NewGuid();

    public ListTeachersUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        _sut = new ListTeachersUseCase(_dbContext, _mockTenantContext.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private async Task<Teacher> SeedTeacherAsync(Guid? centerId = null, string username = "teacher", string displayName = "Teacher", string department = "Toán", UserStatus status = UserStatus.Active, bool softDeletedTeacher = false, bool softDeletedUser = false, UserRole roleName = UserRole.Teacher, ulong rowVersion = 1)
    {
        var targetCenterId = centerId ?? _centerId;
        var userId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var user = new User
        {
            UserId = userId,
            CenterId = targetCenterId,
            Username = username,
            DisplayName = displayName,
            Status = status,
            RoleName = roleName,
            IsDeleted = softDeletedUser,
            PasswordHash = "hashed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var teacher = new Teacher
        {
            TeacherId = userId,
            CenterId = targetCenterId,
            Department = department,
            IsDeleted = softDeletedTeacher,
            User = user,
            RowVersion = rowVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Teachers.Add(teacher);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        return teacher;
    }

    private async Task SeedClassAsync(Guid teacherId, ClassStatus status, bool isDeleted = false)
    {
        var c = new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = teacherId,
            SubjectId = Guid.NewGuid(),
            ClassName = "Class",
            AcademicYear = "2024",
            Status = status,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Classes.Add(c);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ListTeachers_DefaultPagination_ReturnsTeacherDtos()
    {
        await SeedTeacherAsync(username: "t1");
        await SeedTeacherAsync(username: "t2");

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task ListTeachers_Empty_ReturnsEmptyCollection()
    {
        var result = await _sut.ExecuteAsync(new TeacherListQuery());
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task ListTeachers_MapsContractFieldsExactly()
    {
        var teacher = await SeedTeacherAsync(username: "john.doe", displayName: "John Doe", department: "Science", status: UserStatus.Locked);
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Active);

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        var dto = result.Data!.Single();
        Assert.Equal(teacher.TeacherId.ToString("D").ToLowerInvariant(), dto.TeacherId);
        Assert.Equal("john.doe", dto.Username);
        Assert.Equal("John Doe", dto.DisplayName);
        Assert.Equal("Science", dto.Department);
        Assert.Equal(nameof(UserStatus.Locked), dto.Status);
        Assert.Equal(1, dto.ClassCount);
        Assert.Equal(teacher.RowVersion.ToString(CultureInfo.InvariantCulture), dto.RowVersion);
    }

    [Fact]
    public async Task ListTeachers_RowVersionInvariantString()
    {
        var teacher = await SeedTeacherAsync();
        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        var expectedVersion = teacher.RowVersion.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(expectedVersion, result.Data!.First().RowVersion);
    }

    [Fact]
    public async Task ListTeachers_CountsOnlyActiveClasses()
    {
        var teacher = await SeedTeacherAsync();
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Active);
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Active);
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Active, isDeleted: true);

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.First().ClassCount);
    }

    [Fact]
    public async Task ListTeachers_DoesNotCountArchivedClasses()
    {
        var teacher = await SeedTeacherAsync();
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Archived);

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.First().ClassCount);
    }

    [Fact]
    public async Task ListTeachers_SearchByUsername()
    {
        await SeedTeacherAsync(username: "math.teacher");
        await SeedTeacherAsync(username: "science.teacher");

        var result = await _sut.ExecuteAsync(new TeacherListQuery { Search = "math" });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("math.teacher", result.Data![0].Username);
    }

    [Fact]
    public async Task ListTeachers_SearchByDisplayName()
    {
        await SeedTeacherAsync(displayName: "Nguyen Van A");
        await SeedTeacherAsync(displayName: "Le Thi B");

        var result = await _sut.ExecuteAsync(new TeacherListQuery { Search = "Van A" });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("Nguyen Van A", result.Data![0].DisplayName);
    }

    [Fact]
    public async Task ListTeachers_SearchByDepartment()
    {
        await SeedTeacherAsync(department: "Toán");
        await SeedTeacherAsync(department: "Lý");

        var result = await _sut.ExecuteAsync(new TeacherListQuery { Search = "Toán" });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("Toán", result.Data![0].Department);
    }

    [Fact]
    public async Task ListTeachers_StatusFilter()
    {
        await SeedTeacherAsync(status: UserStatus.Active, username: "u1");
        await SeedTeacherAsync(status: UserStatus.Locked, username: "u2");

        var result = await _sut.ExecuteAsync(new TeacherListQuery { Status = UserStatus.Locked });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("u2", result.Data![0].Username);
    }

    [Fact]
    public async Task ListTeachers_PaginationMetadata()
    {
        for (int i = 0; i < 5; i++)
        {
            await SeedTeacherAsync(username: $"u{i}");
        }

        var result = await _sut.ExecuteAsync(new TeacherListQuery { Page = 2, PageSize = 2 });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task ListTeachers_PageBeyondTotal_ReturnsEmptyData()
    {
        await SeedTeacherAsync();

        var result = await _sut.ExecuteAsync(new TeacherListQuery { Page = 2, PageSize = 10 });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task ListTeachers_DeterministicOrdering()
    {
        await SeedTeacherAsync(displayName: "B", username: "u1");
        await SeedTeacherAsync(displayName: "A", username: "u2");
        await SeedTeacherAsync(displayName: "A", username: "u3");

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal("A", result.Data![0].DisplayName);
        Assert.Equal("A", result.Data![1].DisplayName);
        Assert.Equal("B", result.Data![2].DisplayName);

        // Ensure same DisplayName is ordered by TeacherId
        var t1 = result.Data![0];
        var t2 = result.Data![1];
        Assert.True(string.Compare(t1.TeacherId, t2.TeacherId, StringComparison.Ordinal) < 0);
    }

    [Fact]
    public async Task ListTeachers_CrossTenantTeacherExcluded()
    {
        await SeedTeacherAsync();
        await SeedTeacherAsync(centerId: Guid.NewGuid());

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task ListTeachers_SoftDeletedTeacherExcluded()
    {
        await SeedTeacherAsync();
        await SeedTeacherAsync(softDeletedTeacher: true);

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task ListTeachers_SoftDeletedUserExcluded()
    {
        await SeedTeacherAsync();
        await SeedTeacherAsync(softDeletedUser: true);

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task ListTeachers_NonTeacherUserExcluded()
    {
        await SeedTeacherAsync();
        await SeedTeacherAsync(roleName: UserRole.Student);

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task ListTeachers_UnresolvedTenant_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.IsResolved).Returns(false);
        var result = await _sut.ExecuteAsync(new TeacherListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ListTeachers_MissingCenterId_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns((Guid?)null);
        var result = await _sut.ExecuteAsync(new TeacherListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Admin")]
    [InlineData("centermanager")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListTeachers_InvalidRole_ResourceNotFound(string? role)
    {
        _mockTenantContext.Setup(c => c.Role).Returns(role);
        var result = await _sut.ExecuteAsync(new TeacherListQuery());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ListTeachers_InvalidPage_ValidationFailed()
    {
        var result = await _sut.ExecuteAsync(new TeacherListQuery { Page = 0 });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task ListTeachers_InvalidPageSize_ValidationFailed(int pageSize)
    {
        var result = await _sut.ExecuteAsync(new TeacherListQuery { PageSize = pageSize });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ListTeachers_SearchTooLong_ValidationFailed()
    {
        var result = await _sut.ExecuteAsync(new TeacherListQuery { Search = new string('A', 201) });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData((UserStatus)999)]
    [InlineData((UserStatus)(-1))]
    public async Task ListTeachers_UndefinedStatus_ValidationFailed(UserStatus invalidStatus)
    {
        var result = await _sut.ExecuteAsync(new TeacherListQuery { Status = invalidStatus });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ListTeachers_RawSearchOver200WithTrailingSpaces_ValidationFailed()
    {
        var search = new string('A', 200) + " ";
        var result = await _sut.ExecuteAsync(new TeacherListQuery { Search = search });
        
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ListTeachers_Read_DoesNotTrackEntities()
    {
        await SeedTeacherAsync();
        _dbContext.ChangeTracker.Clear();

        var result = await _sut.ExecuteAsync(new TeacherListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public void OrganizationDependencyInjection_ResolvesListTeachersUseCase()
    {
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase("DI").Options;
        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        services.AddSingleton(new EduTwinDbContext(options, mockAccessor.Object));
        services.AddSingleton(new Mock<ITenantContext>().Object);
        services.AddSingleton<TimeProvider>(Mock.Of<TimeProvider>());
        services.AddOrganization();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IListTeachersUseCase>();
        Assert.NotNull(resolved);
        Assert.IsType<ListTeachersUseCase>(resolved);
    }
}
