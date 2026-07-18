using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Organization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class OrganizationOwnershipGuardTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly OrganizationOwnershipGuard _sut;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public OrganizationOwnershipGuardTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
        _mockTenantContext = new Mock<ITenantContext>();

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_userId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        _sut = new OrganizationOwnershipGuard(_dbContext, _mockTenantContext.Object);

        _dbContext.Centers.Add(new Center { CenterId = _centerId, CenterCode = "C1", CenterName = "C1", Status = CenterStatus.Active, Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private Teacher CreateTeacher(Guid id, Guid centerId, bool isDeleted = false)
    {
        return new Teacher { TeacherId = id, CenterId = centerId, IsDeleted = isDeleted, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    }

    private Class CreateClass(Guid id, Guid centerId, Guid teacherId, ClassStatus status = ClassStatus.Active, bool isDeleted = false)
    {
        return new Class { ClassId = id, CenterId = centerId, TeacherId = teacherId, ClassName = "C", AcademicYear = "2025-2026", Status = status, IsDeleted = isDeleted, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    }

    private Student CreateStudent(Guid id, Guid centerId, bool isDeleted = false)
    {
        return new Student { StudentId = id, CenterId = centerId, FullName = "Student", IsDeleted = isDeleted, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    }

    private ClassStudent CreateClassStudent(Guid classId, Guid studentId, Guid centerId, ClassStudentStatus status = ClassStudentStatus.Active)
    {
        return new ClassStudent { ClassId = classId, StudentId = studentId, CenterId = centerId, Status = status, JoinedAt = DateTime.UtcNow };
    }

    // Teacher Guard
    [Fact]
    public async Task CheckTeacherAccessAsync_CenterManager_Allowed()
    {
        var teacher = CreateTeacher(Guid.NewGuid(), _centerId);
        _dbContext.Teachers.Add(teacher);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.CheckTeacherAccessAsync(teacher.TeacherId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Allowed, result);
    }

    [Fact]
    public async Task CheckTeacherAccessAsync_TeacherSelf_Allowed()
    {
        var teacher = CreateTeacher(_userId, _centerId);
        _dbContext.Teachers.Add(teacher);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));

        var result = await _sut.CheckTeacherAccessAsync(teacher.TeacherId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Allowed, result);
    }

    [Fact]
    public async Task CheckTeacherAccessAsync_OtherTeacher_Forbidden()
    {
        var otherTeacher = CreateTeacher(Guid.NewGuid(), _centerId);
        _dbContext.Teachers.Add(otherTeacher);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));

        var result = await _sut.CheckTeacherAccessAsync(otherTeacher.TeacherId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Forbidden, result);
    }

    [Fact]
    public async Task CheckTeacherAccessAsync_StudentRole_Forbidden()
    {
        var teacher = CreateTeacher(Guid.NewGuid(), _centerId);
        _dbContext.Teachers.Add(teacher);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.CheckTeacherAccessAsync(teacher.TeacherId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Forbidden, result);
    }

    [Fact]
    public async Task CheckTeacherAccessAsync_CrossTenant_NotFound()
    {
        var otherCenterId = Guid.NewGuid();
        var teacher = CreateTeacher(Guid.NewGuid(), otherCenterId);

        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, CenterCode = "C2", CenterName = "C2", Status = CenterStatus.Active, Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Teachers.Add(teacher);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CheckTeacherAccessAsync(teacher.TeacherId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.NotFound, result);
    }

    [Fact]
    public async Task CheckTeacherAccessAsync_SoftDeleted_NotFound()
    {
        var teacher = CreateTeacher(Guid.NewGuid(), _centerId, isDeleted: true);
        _dbContext.Teachers.Add(teacher);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CheckTeacherAccessAsync(teacher.TeacherId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.NotFound, result);
    }

    // Class Guard
    [Fact]
    public async Task CheckClassAccessAsync_CenterManager_Allowed()
    {
        var cls = CreateClass(Guid.NewGuid(), _centerId, Guid.NewGuid());
        _dbContext.Classes.Add(cls);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));
        var result = await _sut.CheckClassAccessAsync(cls.ClassId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Allowed, result);
    }

    [Fact]
    public async Task CheckClassAccessAsync_TeacherOwnClass_Allowed()
    {
        var cls = CreateClass(Guid.NewGuid(), _centerId, _userId);
        _dbContext.Classes.Add(cls);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        var result = await _sut.CheckClassAccessAsync(cls.ClassId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Allowed, result);
    }

    [Fact]
    public async Task CheckClassAccessAsync_OtherTeacher_Forbidden()
    {
        var cls = CreateClass(Guid.NewGuid(), _centerId, Guid.NewGuid());
        _dbContext.Classes.Add(cls);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        var result = await _sut.CheckClassAccessAsync(cls.ClassId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Forbidden, result);
    }

    [Fact]
    public async Task CheckClassAccessAsync_StudentRole_Forbidden()
    {
        var cls = CreateClass(Guid.NewGuid(), _centerId, Guid.NewGuid());
        _dbContext.Classes.Add(cls);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Student));
        var result = await _sut.CheckClassAccessAsync(cls.ClassId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Forbidden, result);
    }

    [Fact]
    public async Task CheckClassAccessAsync_CrossTenant_NotFound()
    {
        var otherCenterId = Guid.NewGuid();
        var cls = CreateClass(Guid.NewGuid(), otherCenterId, Guid.NewGuid());
        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, CenterCode = "C2", CenterName = "C2", Status = CenterStatus.Active, Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Classes.Add(cls);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CheckClassAccessAsync(cls.ClassId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.NotFound, result);
    }

    [Fact]
    public async Task CheckClassAccessAsync_SoftDeleted_NotFound()
    {
        var cls = CreateClass(Guid.NewGuid(), _centerId, Guid.NewGuid(), isDeleted: true);
        _dbContext.Classes.Add(cls);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CheckClassAccessAsync(cls.ClassId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.NotFound, result);
    }

    [Fact]
    public async Task CheckClassAccessAsync_ArchivedOwnClass_Allowed()
    {
        var cls = CreateClass(Guid.NewGuid(), _centerId, _userId, status: ClassStatus.Archived);
        _dbContext.Classes.Add(cls);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        var result = await _sut.CheckClassAccessAsync(cls.ClassId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Allowed, result);
    }

    // Student Guard
    [Fact]
    public async Task CheckStudentAccessAsync_CenterManager_Allowed()
    {
        var student = CreateStudent(Guid.NewGuid(), _centerId);
        _dbContext.Students.Add(student);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));
        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Allowed, result);
    }

    [Fact]
    public async Task CheckStudentAccessAsync_StudentSelf_Allowed()
    {
        var student = CreateStudent(_userId, _centerId);
        _dbContext.Students.Add(student);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Student));
        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Allowed, result);
    }

    [Fact]
    public async Task CheckStudentAccessAsync_OtherStudent_Forbidden()
    {
        var student = CreateStudent(Guid.NewGuid(), _centerId);
        _dbContext.Students.Add(student);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Student));
        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Forbidden, result);
    }

    [Fact]
    public async Task CheckStudentAccessAsync_TeacherWithActiveMembershipAndClass_Allowed()
    {
        var student = CreateStudent(Guid.NewGuid(), _centerId);
        var cls = CreateClass(Guid.NewGuid(), _centerId, _userId);
        var classStudent = CreateClassStudent(cls.ClassId, student.StudentId, _centerId);

        _dbContext.Students.Add(student);
        _dbContext.Classes.Add(cls);
        _dbContext.ClassStudents.Add(classStudent);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Allowed, result);
    }

    [Fact]
    public async Task CheckStudentAccessAsync_OtherTeacher_Forbidden()
    {
        var student = CreateStudent(Guid.NewGuid(), _centerId);
        var cls = CreateClass(Guid.NewGuid(), _centerId, Guid.NewGuid());
        var classStudent = CreateClassStudent(cls.ClassId, student.StudentId, _centerId);

        _dbContext.Students.Add(student);
        _dbContext.Classes.Add(cls);
        _dbContext.ClassStudents.Add(classStudent);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Forbidden, result);
    }

    [Fact]
    public async Task CheckStudentAccessAsync_MembershipRemoved_Forbidden()
    {
        var student = CreateStudent(Guid.NewGuid(), _centerId);
        var cls = CreateClass(Guid.NewGuid(), _centerId, _userId);
        var classStudent = CreateClassStudent(cls.ClassId, student.StudentId, _centerId, status: ClassStudentStatus.Removed);

        _dbContext.Students.Add(student);
        _dbContext.Classes.Add(cls);
        _dbContext.ClassStudents.Add(classStudent);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Forbidden, result);
    }

    [Fact]
    public async Task CheckStudentAccessAsync_MembershipThroughArchivedClass_Forbidden()
    {
        var student = CreateStudent(Guid.NewGuid(), _centerId);
        var cls = CreateClass(Guid.NewGuid(), _centerId, _userId, status: ClassStatus.Archived);
        var classStudent = CreateClassStudent(cls.ClassId, student.StudentId, _centerId);

        _dbContext.Students.Add(student);
        _dbContext.Classes.Add(cls);
        _dbContext.ClassStudents.Add(classStudent);
        await _dbContext.SaveChangesAsync();

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.Forbidden, result);
    }

    [Fact]
    public async Task CheckStudentAccessAsync_CrossTenant_NotFound()
    {
        var otherCenterId = Guid.NewGuid();
        var student = CreateStudent(Guid.NewGuid(), otherCenterId);

        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, CenterCode = "C2", CenterName = "C2", Status = CenterStatus.Active, Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Students.Add(student);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.NotFound, result);
    }

    [Fact]
    public async Task CheckStudentAccessAsync_SoftDeleted_NotFound()
    {
        var student = CreateStudent(Guid.NewGuid(), _centerId, isDeleted: true);
        _dbContext.Students.Add(student);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.CheckStudentAccessAsync(student.StudentId, CancellationToken.None);
        Assert.Equal(OwnershipDecision.NotFound, result);
    }

    // Fail-closed
    [Fact]
    public async Task FailClosed_UnresolvedTenantContext_NotFound()
    {
        _mockTenantContext.Setup(c => c.IsResolved).Returns(false);
        var result = await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(OwnershipDecision.NotFound, result);
    }

    [Fact]
    public async Task FailClosed_EmptyGuid_NotFound()
    {
        var result = await _sut.CheckTeacherAccessAsync(Guid.Empty, CancellationToken.None);
        Assert.Equal(OwnershipDecision.NotFound, result);
    }

    [Fact]
    public async Task FailClosed_MissingUserId_NotFound()
    {
        _mockTenantContext.Setup(c => c.UserId).Returns((Guid?)null);
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckClassAccessAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckStudentAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FailClosed_EmptyUserId_NotFound()
    {
        _mockTenantContext.Setup(c => c.UserId).Returns(Guid.Empty);
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FailClosed_MissingCenterId_NotFound()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns((Guid?)null);
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FailClosed_EmptyCenterId_NotFound()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns(Guid.Empty);
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FailClosed_NullRole_NotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns((string?)null);
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FailClosed_EmptyRole_NotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns("   ");
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FailClosed_AdminRole_NotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns("Admin");
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckClassAccessAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckStudentAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task FailClosed_WrongCasingRole_NotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns("teacher");
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    public async Task FailClosed_NumericRole_NotFound(string role)
    {
        _mockTenantContext.Setup(c => c.Role).Returns(role);
        Assert.Equal(OwnershipDecision.NotFound, await _sut.CheckTeacherAccessAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public void DependencyInjection_ShouldResolveInterfacesToSameScopedInstance()
    {
        var services = new ServiceCollection();
        services.AddScoped(sp => _dbContext);
        services.AddScoped(sp => _mockTenantContext.Object);
        services.AddScoped<ITenantContextInitializer>(sp => new Mock<ITenantContextInitializer>().Object);
        services.AddScoped<IBackgroundTenantScopeFactory>(sp => new Mock<IBackgroundTenantScopeFactory>().Object);
        services.AddScoped<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>(sp => new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>().Object);

        services.AddIdentityAndTenancy();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var teacherGuard = scope.ServiceProvider.GetRequiredService<ITeacherOwnershipGuard>();
        var classGuard = scope.ServiceProvider.GetRequiredService<IClassOwnershipGuard>();
        var studentGuard = scope.ServiceProvider.GetRequiredService<IStudentOwnershipGuard>();

        Assert.NotNull(teacherGuard);
        Assert.NotNull(classGuard);
        Assert.NotNull(studentGuard);

        Assert.Same(teacherGuard, classGuard);
        Assert.Same(teacherGuard, studentGuard);
    }
}
