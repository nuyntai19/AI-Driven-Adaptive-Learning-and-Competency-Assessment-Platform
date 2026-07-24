using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class CreateCurriculumUseCaseTests
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CreateCurriculumUseCase _sut;
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly ITenantIdAccessor _tenantAccessor;

    public CreateCurriculumUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);
        _tenantAccessor = tenantAccessorMock.Object;

        _timeProviderMock = new Mock<TimeProvider>();
        var utcNow = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(utcNow);

        _dbContext = new EduTwinDbContext(_options, _tenantAccessor);
        _sut = new CreateCurriculumUseCase(_dbContext, _tenantMock.Object, _timeProviderMock.Object);
    }

    private void SetupTenant(Guid centerId, Guid userId, string role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

    private static long _nodeIdCounter = 100;

    private async Task<(Center Center, User TeacherUser, Teacher Teacher, Subject Subject, KnowledgeNode Node1, KnowledgeNode Node2)> SeedBasicEntitiesAsync(
        Guid? customCenterId = null,
        Guid? customTeacherId = null)
    {
        var centerId = customCenterId ?? Guid.NewGuid();
        var teacherId = customTeacherId ?? Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var id1 = (ulong)Interlocked.Increment(ref _nodeIdCounter);
        var id2 = (ulong)Interlocked.Increment(ref _nodeIdCounter);

        var center = new Center
        {
            CenterId = centerId,
            CenterCode = "CENTER-" + centerId.ToString()[..8],
            CenterName = "Test Center",
            Status = CenterStatus.Active,
            Timezone = "UTC",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var teacherUser = new User
        {
            UserId = teacherId,
            CenterId = centerId,
            Username = "teacher-" + teacherId.ToString()[..8],
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            DisplayName = "Teacher One",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var teacher = new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var subject = new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "MATH12",
            SubjectName = "Toán 12",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var node1 = new KnowledgeNode
        {
            NodeId = id1,
            CenterId = centerId,
            SubjectId = subjectId,
            NodeType = NodeType.Topic,
            NodeCode = "N" + id1,
            NodeName = "Node " + id1,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var node2 = new KnowledgeNode
        {
            NodeId = id2,
            CenterId = centerId,
            SubjectId = subjectId,
            NodeType = NodeType.Topic,
            NodeCode = "N" + id2,
            NodeName = "Node " + id2,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(teacherUser);
        _dbContext.Teachers.Add(teacher);
        _dbContext.Subjects.Add(subject);
        _dbContext.KnowledgeNodes.AddRange(node1, node2);
        await _dbContext.SaveChangesAsync();

        return (center, teacherUser, teacher, subject, node1, node2);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherOwner_ValidRequest_CreatesCurriculumSuccessfully()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var n1Str = seed.Node1.NodeId.ToString(CultureInfo.InvariantCulture);
        var n2Str = seed.Node2.NodeId.ToString(CultureInfo.InvariantCulture);
        var fixedTime = _timeProviderMock.Object.GetUtcNow().UtcDateTime;

        var request = new CreateCurriculumRequest
        {
            TeacherId = null,
            SubjectId = seed.Subject.SubjectId,
            Title = "Lộ trình Toán 12",
            Description = "Giáo trình nhập thủ công",
            NodeIds = new List<string> { n1Str, n2Str }
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        // Assert DTO projections
        Assert.True(Guid.TryParse(result.Data.CurriculumId, out var parsedCurriculumGuid));
        Assert.Equal(parsedCurriculumGuid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(), result.Data.CurriculumId);
        Assert.Equal(teacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(), result.Data.TeacherId);
        Assert.Equal(seed.Subject.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(), result.Data.SubjectId);
        Assert.Equal("Lộ trình Toán 12", result.Data.Title);
        Assert.Equal("Giáo trình nhập thủ công", result.Data.Description);
        Assert.Null(result.Data.SourceFile);
        Assert.Equal("Draft", result.Data.ReviewStatus);
        Assert.Empty(result.Data.ClassIds);
        Assert.Equal(new List<string> { n1Str, n2Str }, result.Data.NodeIds);
        Assert.Equal("1", result.Data.RowVersion);

        // Assert Curriculum DB Persistence
        var curriculumInDb = await _dbContext.Curriculums.SingleOrDefaultAsync();
        Assert.NotNull(curriculumInDb);
        Assert.Equal(parsedCurriculumGuid, curriculumInDb.CurriculumId);
        Assert.Equal(centerId, curriculumInDb.CenterId);
        Assert.Equal(teacherId, curriculumInDb.TeacherId);
        Assert.Equal(seed.Subject.SubjectId, curriculumInDb.SubjectId);
        Assert.Equal("Lộ trình Toán 12", curriculumInDb.Title);
        Assert.Equal("Giáo trình nhập thủ công", curriculumInDb.Description);
        Assert.Null(curriculumInDb.SourceFile);
        Assert.Equal(ReviewStatus.Draft, curriculumInDb.ReviewStatus);
        Assert.False(curriculumInDb.IsDeleted);
        Assert.Equal(1ul, curriculumInDb.RowVersion);
        Assert.Equal(fixedTime, curriculumInDb.CreatedAt);
        Assert.Equal(fixedTime, curriculumInDb.UpdatedAt);
        Assert.Equal(teacherId, curriculumInDb.CreatedBy);
        Assert.Equal(teacherId, curriculumInDb.UpdatedBy);

        // Assert CurriculumNode DB Persistence
        var nodesInDb = await _dbContext.CurriculumNodes.OrderBy(cn => cn.OrderIndex).ToListAsync();
        Assert.Equal(2, nodesInDb.Count);
        Assert.Equal(centerId, nodesInDb[0].CenterId);
        Assert.Equal(parsedCurriculumGuid, nodesInDb[0].CurriculumId);
        Assert.Equal(seed.Node1.NodeId, nodesInDb[0].NodeId);
        Assert.Equal(1u, nodesInDb[0].OrderIndex);
        Assert.Equal(fixedTime, nodesInDb[0].CreatedAt);

        Assert.Equal(centerId, nodesInDb[1].CenterId);
        Assert.Equal(parsedCurriculumGuid, nodesInDb[1].CurriculumId);
        Assert.Equal(seed.Node2.NodeId, nodesInDb[1].NodeId);
        Assert.Equal(2u, nodesInDb[1].OrderIndex);
        Assert.Equal(fixedTime, nodesInDb[1].CreatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerOwner_ValidTeacher_CreatesCurriculumSuccessfully()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var n1Str = seed.Node1.NodeId.ToString(CultureInfo.InvariantCulture);
        var n2Str = seed.Node2.NodeId.ToString(CultureInfo.InvariantCulture);

        var request = new CreateCurriculumRequest
        {
            TeacherId = teacherId.ToString("D"),
            SubjectId = seed.Subject.SubjectId,
            Title = "Lộ trình Toán 12 do Manager tạo",
            Description = "Giáo trình",
            NodeIds = new List<string> { n2Str, n1Str }
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(teacherId.ToString("D").ToLowerInvariant(), result.Data.TeacherId);
        Assert.Equal(new List<string> { n2Str, n1Str }, result.Data.NodeIds);

        var nodesInDb = await _dbContext.CurriculumNodes.OrderBy(cn => cn.OrderIndex).ToListAsync();
        Assert.Equal(seed.Node2.NodeId, nodesInDb[0].NodeId);
        Assert.Equal(1u, nodesInDb[0].OrderIndex);
        Assert.Equal(seed.Node1.NodeId, nodesInDb[1].NodeId);
        Assert.Equal(2u, nodesInDb[1].OrderIndex);
    }

    // --- Section B: Tenant & Center Tests ---

    [Fact]
    public async Task ExecuteAsync_UnresolvedTenant_ReturnsResourceNotFound_NoPersistence()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);
        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string>() };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCenterId_ReturnsResourceNotFound_NoPersistence()
    {
        SetupTenant(Guid.Empty, Guid.NewGuid(), nameof(UserRole.Teacher));
        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string>() };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUserId_ReturnsResourceNotFound_NoPersistence()
    {
        SetupTenant(Guid.NewGuid(), Guid.Empty, nameof(UserRole.Teacher));
        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string>() };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_MissingCenter_ReturnsResourceNotFound_NoPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var teacherUser = new User
        {
            UserId = teacherId,
            CenterId = centerId,
            Username = "teacher-" + teacherId.ToString()[..8],
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            DisplayName = "Teacher One",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var teacher = new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var subject = new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "MATH12",
            SubjectName = "Toán 12",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(teacherUser);
        _dbContext.Teachers.Add(teacher);
        _dbContext.Subjects.Add(subject);
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = subjectId, Title = "Title", NodeIds = new List<string>() };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_InactiveCenter_ReturnsResourceNotFound_NoPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.Center.Status = CenterStatus.Suspended;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_DeletedCenter_ReturnsResourceNotFound_NoPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.Center.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Student")]
    [InlineData("teacher")]
    [InlineData("ADMIN")]
    public async Task ExecuteAsync_InvalidOrWrongCasingRole_ReturnsResourceNotFound(string? role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    // --- Section C: Teacher Caller Predicate Tests ---

    [Theory]
    [InlineData("2a584ad0-6ea5-4ff7-a3a9-9baf8cbc2036")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ExecuteAsync_TeacherCaller_SendsNonNullTeacherId_ReturnsValidationFailed(string teacherId)
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupTenant(centerId, userId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, userId);

        var request = new CreateCurriculumRequest
        {
            TeacherId = teacherId,
            SubjectId = seed.Subject.SubjectId,
            Title = "Title",
            NodeIds = new List<string>()
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherProfileMissing_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupTenant(centerId, userId, nameof(UserRole.Teacher));

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

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

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

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
        seed.TeacherUser.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

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
        seed.TeacherUser.Status = UserStatus.Locked;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

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
        seed.TeacherUser.Status = UserStatus.Disabled;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

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
        seed.TeacherUser.RoleName = UserRole.Student;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // --- Section D: CenterManager Designated Teacher Predicate Tests ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ExecuteAsync_CenterManager_InvalidTeacherId_ReturnsValidationFailed(string? teacherId)
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var request = new CreateCurriculumRequest { TeacherId = teacherId, SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_TeacherProfileMissing_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var randomTeacherId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var seed = await SeedBasicEntitiesAsync(centerId, managerId);

        var request = new CreateCurriculumRequest { TeacherId = randomTeacherId.ToString("D"), SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_TeacherProfileDeleted_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.Teacher.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { TeacherId = teacherId.ToString("D"), SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_TeacherLinkedUserDeleted_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.TeacherUser.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { TeacherId = teacherId.ToString("D"), SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_TeacherLinkedUserLocked_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.TeacherUser.Status = UserStatus.Locked;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { TeacherId = teacherId.ToString("D"), SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_TeacherLinkedUserDisabled_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.TeacherUser.Status = UserStatus.Disabled;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { TeacherId = teacherId.ToString("D"), SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_TeacherLinkedUserWrongRole_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);
        seed.TeacherUser.RoleName = UserRole.Student;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { TeacherId = teacherId.ToString("D"), SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_CrossTenantTeacher_ReturnsResourceNotFound()
    {
        var centerId1 = Guid.NewGuid();
        var centerId2 = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId1, managerId, nameof(UserRole.CenterManager));

        await SeedBasicEntitiesAsync(centerId2, teacherId); // Teacher in center2
        var seed1 = await SeedBasicEntitiesAsync(centerId1, managerId); // Subject in center1

        var request = new CreateCurriculumRequest { TeacherId = teacherId.ToString("D"), SubjectId = seed1.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // --- Section E: Subject References Tests ---

    [Fact]
    public async Task ExecuteAsync_MissingSubject_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        await SeedBasicEntitiesAsync(centerId, teacherId);

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_InactiveSubject_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        seed.Subject.IsActive = false;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_DeletedSubject_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        seed.Subject.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantSubject_ReturnsResourceNotFound()
    {
        var centerId1 = Guid.NewGuid();
        var centerId2 = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId1, teacherId, nameof(UserRole.Teacher));

        await SeedBasicEntitiesAsync(centerId1, teacherId);
        var seed2 = await SeedBasicEntitiesAsync(centerId2, Guid.NewGuid());

        var request = new CreateCurriculumRequest { SubjectId = seed2.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    // --- Section F: Knowledge Node References Tests ---

    [Fact]
    public async Task ExecuteAsync_CrossTenantNode_ReturnsResourceNotFound_NoPersistence()
    {
        var centerId1 = Guid.NewGuid();
        var centerId2 = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId1, teacherId, nameof(UserRole.Teacher));

        var seed1 = await SeedBasicEntitiesAsync(centerId1, teacherId);
        var seed2 = await SeedBasicEntitiesAsync(centerId2, Guid.NewGuid());

        // Assert fixture node actually exists in DB under center2
        var crossNodeInDb = await _dbContext.KnowledgeNodes.FindAsync(seed2.Node1.NodeId);
        Assert.NotNull(crossNodeInDb);
        Assert.Equal(centerId2, crossNodeInDb.CenterId);

        var request = new CreateCurriculumRequest
        {
            SubjectId = seed1.Subject.SubjectId,
            Title = "Title",
            NodeIds = new List<string> { seed2.Node1.NodeId.ToString(CultureInfo.InvariantCulture) }
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_WrongSubjectNode_ReturnsResourceNotFound_NoPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed1 = await SeedBasicEntitiesAsync(centerId, teacherId);

        // Create second subject in same center and seed node in second subject
        var subject2 = new Subject
        {
            SubjectId = Guid.NewGuid(),
            CenterId = centerId,
            SubjectCode = "ENG12",
            SubjectName = "Tiếng Anh 12",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var wrongSubjectNodeId = (ulong)Interlocked.Increment(ref _nodeIdCounter);
        var wrongSubjectNode = new KnowledgeNode
        {
            NodeId = wrongSubjectNodeId,
            CenterId = centerId,
            SubjectId = subject2.SubjectId,
            NodeType = NodeType.Topic,
            NodeCode = "N" + wrongSubjectNodeId,
            NodeName = "Wrong Subject Node",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Subjects.Add(subject2);
        _dbContext.KnowledgeNodes.Add(wrongSubjectNode);
        await _dbContext.SaveChangesAsync();

        // Assert fixture node exists in DB under subject2
        var nodeInDb = await _dbContext.KnowledgeNodes.FindAsync(wrongSubjectNodeId);
        Assert.NotNull(nodeInDb);
        Assert.Equal(subject2.SubjectId, nodeInDb.SubjectId);

        var request = new CreateCurriculumRequest
        {
            SubjectId = seed1.Subject.SubjectId, // Calling for subject1
            Title = "Title",
            NodeIds = new List<string> { wrongSubjectNodeId.ToString(CultureInfo.InvariantCulture) }
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_InactiveNode_ReturnsResourceNotFound_NoPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        seed.Node1.IsActive = false;
        await _dbContext.SaveChangesAsync();

        // Assert fixture node exists in DB with IsActive = false
        var nodeInDb = await _dbContext.KnowledgeNodes.FindAsync(seed.Node1.NodeId);
        Assert.NotNull(nodeInDb);
        Assert.False(nodeInDb.IsActive);

        var request = new CreateCurriculumRequest
        {
            SubjectId = seed.Subject.SubjectId,
            Title = "Title",
            NodeIds = new List<string> { seed.Node1.NodeId.ToString(CultureInfo.InvariantCulture) }
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_DeletedNode_ReturnsResourceNotFound_NoPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        seed.Node1.IsDeleted = true;
        await _dbContext.SaveChangesAsync();

        // Assert fixture node exists in DB with IsDeleted = true
        var nodeInDb = await _dbContext.KnowledgeNodes.FindAsync(seed.Node1.NodeId);
        Assert.NotNull(nodeInDb);
        Assert.True(nodeInDb.IsDeleted);

        var request = new CreateCurriculumRequest
        {
            SubjectId = seed.Subject.SubjectId,
            Title = "Title",
            NodeIds = new List<string> { seed.Node1.NodeId.ToString(CultureInfo.InvariantCulture) }
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    // --- Section G & Input Validation Tests ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_InvalidTitle_ReturnsValidationFailed(string? title)
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = title, NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_RawTitleOver250_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = new string('A', 251), NodeIds = new List<string>() };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_NullNodeIds_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = null };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("0")]
    [InlineData("18446744073709551616")] // ulong overflow
    public async Task ExecuteAsync_InvalidRawNodeIdFormat_ReturnsValidationFailed(string rawNodeId)
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string> { rawNodeId } };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateNodeIdsExact_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string> { "100", "100" } };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateNodeIdsNumericEquivalent_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Title", NodeIds = new List<string> { "1", "01" } };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_OneNodeInvalid_ReturnsResourceNotFound_NoPartialPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var n1Str = seed.Node1.NodeId.ToString(CultureInfo.InvariantCulture);

        var request = new CreateCurriculumRequest
        {
            SubjectId = seed.Subject.SubjectId,
            Title = "Title",
            NodeIds = new List<string> { n1Str, "99999999" }
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    // --- Section H: Persistence & Exceptions Tests ---

    [Fact]
    public async Task ExecuteAsync_TwoCurriculumsSameTitle_BothCreatedSuccessfully()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var request1 = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Giáo trình Toán", NodeIds = new List<string>() };
        var request2 = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Giáo trình Toán", NodeIds = new List<string>() };

        var result1 = await _sut.ExecuteAsync(request1);
        var result2 = await _sut.ExecuteAsync(request2);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.NotEqual(result1.Data!.CurriculumId, result2.Data!.CurriculumId);
        Assert.Equal(2, await _dbContext.Curriculums.CountAsync());
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedDbUpdateException_Rethrows_NoPartialPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        var dbEx = new DbUpdateException("DB Error", new Exception("Foreign Key Constraint Failure"));
        var faultyContext = new FaultyDbContext(_options, _tenantAccessor, dbEx);
        var faultySut = new CreateCurriculumUseCase(faultyContext, _tenantMock.Object, _timeProviderMock.Object);

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };

        await Assert.ThrowsAsync<DbUpdateException>(() => faultySut.ExecuteAsync(request));

        Assert.Empty(await faultyContext.Curriculums.ToListAsync());
        Assert.Empty(await faultyContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_HonorsCancellationToken_NoPartialPersistence()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var seed = await SeedBasicEntitiesAsync(centerId, teacherId);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new CreateCurriculumRequest { SubjectId = seed.Subject.SubjectId, Title = "Title", NodeIds = new List<string>() };
        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ExecuteAsync(request, cts.Token));

        Assert.Empty(await _dbContext.Curriculums.ToListAsync());
        Assert.Empty(await _dbContext.CurriculumNodes.ToListAsync());
    }

    [Fact]
    public void ProductionSourceFile_Exists_AndDoesNotUseIgnoreQueryFilters()
    {
        var currentDir = AppContext.BaseDirectory;
        var rootDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", ".."));
        var sourcePath = Path.Combine(rootDir, "src", "EduTwin.BLL", "CurriculumAndQuestions", "CreateCurriculumUseCase.cs");
        Assert.True(File.Exists(sourcePath), $"File source not found at: {sourcePath}");

        var content = File.ReadAllText(sourcePath);
        Assert.DoesNotContain("IgnoreQueryFilters", content);
    }

    private class FaultyDbContext : EduTwinDbContext
    {
        private readonly Exception _exceptionToThrow;

        public FaultyDbContext(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantAccessor, Exception exceptionToThrow)
            : base(options, tenantAccessor)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw _exceptionToThrow;
        }
    }
}
