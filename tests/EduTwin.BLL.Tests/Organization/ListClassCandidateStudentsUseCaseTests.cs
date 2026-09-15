using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

using Microsoft.EntityFrameworkCore.Diagnostics;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.Organization;

public class ListClassCandidateStudentsUseCaseTests
{
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<ITenantContext> _mockTenantContext = new();
    private readonly Mock<IClassOwnershipGuard> _mockOwnershipGuard = new();
    private readonly DateTimeOffset _fixedTime = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public ListClassCandidateStudentsUseCaseTests()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_userId);
        _mockTenantContext.Setup(c => c.Role).Returns("CenterManager");
    }

    private EduTwinDbContext CreateContext(string dbName, Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(tenantId ?? _centerId);
        return new EduTwinDbContext(options, mockAccessor.Object);
    }

    private async Task SeedCenterAndClassAsync(EduTwinDbContext dbContext, Guid classId, bool isClassDeleted = false)
    {
        dbContext.Centers.Add(new Center
        {
            CenterId = _centerId,
            CenterName = "Test Center",
            CenterCode = "TC",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        dbContext.Classes.Add(new Class
        {
            ClassId = classId,
            CenterId = _centerId,
            ClassName = "Class 10A",
            AcademicYear = "2025-2026",
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            Status = ClassStatus.Active,
            IsDeleted = isClassDeleted,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedStudentAsync(
        EduTwinDbContext dbContext,
        Guid studentId,
        string username = "student1",
        string fullName = "Student One",
        UserStatus userStatus = UserStatus.Active,
        bool isDeleted = false,
        Guid? classId = null,
        ClassStudentStatus membershipStatus = ClassStudentStatus.Active)
    {
        var user = new User
        {
            UserId = studentId,
            CenterId = _centerId,
            Username = username,
            DisplayName = fullName,
            PasswordHash = "hash",
            Status = userStatus,
            RoleName = UserRole.Student,
            IsDeleted = isDeleted,
            AuthVersion = 1,
            RowVersion = 1,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        dbContext.Users.Add(user);

        var student = new Student
        {
            StudentId = studentId,
            CenterId = _centerId,
            FullName = fullName,
            GradeLevel = 10,
            IsDeleted = isDeleted,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        dbContext.Students.Add(student);

        if (classId.HasValue)
        {
            dbContext.ClassStudents.Add(new ClassStudent
            {
                CenterId = _centerId,
                ClassId = classId.Value,
                StudentId = studentId,
                Status = membershipStatus,
                JoinedAt = _fixedTime.UtcDateTime
            });
        }

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task EmptyClassId_ReturnsValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        await SeedCenterAndClassAsync(dbContext, Guid.NewGuid());
        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);

        var result = await sut.ExecuteAsync(Guid.Empty, new CandidateStudentListQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task InvalidPagination_ReturnsValidationFailed(int page, int pageSize)
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var classId = Guid.NewGuid();
        await SeedCenterAndClassAsync(dbContext, classId);

        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId, new CandidateStudentListQuery { Page = page, PageSize = pageSize }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task OwnershipNotFound_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var classId = Guid.NewGuid();
        await SeedCenterAndClassAsync(dbContext, classId);

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.NotFound);

        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId, new CandidateStudentListQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task OwnershipForbidden_ReturnsForbiddenResource()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var classId = Guid.NewGuid();
        await SeedCenterAndClassAsync(dbContext, classId);

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Forbidden);

        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId, new CandidateStudentListQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task DeletedClass_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var classId = Guid.NewGuid();
        await SeedCenterAndClassAsync(dbContext, classId, isClassDeleted: true);

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId, new CandidateStudentListQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ActiveOnly_OnlyActiveStudentsReturned_LockedAndDisabledExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var classId = Guid.NewGuid();
        await SeedCenterAndClassAsync(dbContext, classId);

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var sActive = Guid.NewGuid();
        var sLocked = Guid.NewGuid();
        var sDisabled = Guid.NewGuid();

        await SeedStudentAsync(dbContext, sActive, "active_user", "Active Student", UserStatus.Active);
        await SeedStudentAsync(dbContext, sLocked, "locked_user", "Locked Student", UserStatus.Locked);
        await SeedStudentAsync(dbContext, sDisabled, "disabled_user", "Disabled Student", UserStatus.Disabled);

        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId, new CandidateStudentListQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(sActive, result.Data![0].StudentId);
        Assert.Equal("Active", result.Data![0].Status);
    }

    [Fact]
    public async Task ClassActiveMembersExcluded_RemovedMembersAndNonMembersIncluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var classId = Guid.NewGuid();
        await SeedCenterAndClassAsync(dbContext, classId);

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var sActiveMember = Guid.NewGuid();
        var sRemovedMember = Guid.NewGuid();
        var sNonMember = Guid.NewGuid();

        await SeedStudentAsync(dbContext, sActiveMember, "member1", "Active Member", UserStatus.Active, classId: classId, membershipStatus: ClassStudentStatus.Active);
        await SeedStudentAsync(dbContext, sRemovedMember, "removed1", "Removed Member", UserStatus.Active, classId: classId, membershipStatus: ClassStudentStatus.Removed);
        await SeedStudentAsync(dbContext, sNonMember, "nonmember1", "Non Member", UserStatus.Active);

        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId, new CandidateStudentListQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.DoesNotContain(result.Data, s => s.StudentId == sActiveMember);
        Assert.Contains(result.Data, s => s.StudentId == sRemovedMember);
        Assert.Contains(result.Data, s => s.StudentId == sNonMember);
    }

    [Fact]
    public async Task SearchFilter_FiltersByUsernameOrFullName()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var classId = Guid.NewGuid();
        await SeedCenterAndClassAsync(dbContext, classId);

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var s3 = Guid.NewGuid();

        await SeedStudentAsync(dbContext, s1, "alice_smith", "Alice Smith");
        await SeedStudentAsync(dbContext, s2, "bob_jones", "Bob Jones");
        await SeedStudentAsync(dbContext, s3, "charlie_smith", "Charlie Doe");

        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId, new CandidateStudentListQuery { Search = "smith" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.Contains(result.Data, s => s.StudentId == s1); // FullName matches "smith"
        Assert.Contains(result.Data, s => s.StudentId == s3); // Username matches "smith"
        Assert.DoesNotContain(result.Data, s => s.StudentId == s2);
    }

    [Fact]
    public async Task PassesExactCancellationToken()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var classId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new ListClassCandidateStudentsUseCase(_mockTenantContext.Object, dbContext, _mockOwnershipGuard.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.ExecuteAsync(classId, new CandidateStudentListQuery(), cts.Token));
    }
}
