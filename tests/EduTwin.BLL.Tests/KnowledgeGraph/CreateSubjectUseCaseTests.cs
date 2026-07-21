using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence.Tenancy;
using System.Globalization;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class CreateSubjectUseCaseTests
{
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly TimeProvider _timeProvider;
    private readonly EduTwinDbContext _dbContext;
    private readonly CreateSubjectUseCase _sut;

    public CreateSubjectUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();

        tenantIdAccessorMock.SetupGet(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        _dbContext = new EduTwinDbContext(options, tenantIdAccessorMock.Object);

        _timeProvider = TimeProvider.System;
        _sut = new CreateSubjectUseCase(_dbContext, _tenantMock.Object, _timeProvider);
    }

    private async Task SetupActiveCenter(Guid centerId)
    {
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "C01",
            CenterName = "Test Center",
            Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task TenantUnresolved_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);
        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CenterIdNullOrEmpty_ReturnsResourceNotFound(bool isNull)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(isNull ? null : Guid.Empty);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));
        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UserIdNullOrEmpty_ReturnsResourceNotFound(bool isNull)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(isNull ? null : Guid.Empty);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));
        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Student")]
    [InlineData("centerManager")] // wrong casing
    [InlineData("0")] // numeric
    public async Task InvalidRole_ReturnsResourceNotFound(string? role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role!);
        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SuspendedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center {
            CenterId = centerId,
            CenterCode = "C01", CenterName = "A", Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Suspended,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center {
            CenterId = centerId,
            CenterCode = "C01", CenterName = "A", Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.CenterManager))]
    public async Task ValidRole_Success(string role)
    {
        var centerId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "MATH", SubjectName = "Toan" });
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RawLengthValidation_HappensBeforeTrim()
    {
        // 32 chars of 'A' + 1 space = 33 raw length
        var code = new string('A', 32) + " ";
        var request = new CreateSubjectRequest { SubjectCode = code, SubjectName = "Valid" };

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Values_AreTrimmedAndNormalized()
    {
        var centerId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(new CreateSubjectRequest
        {
            SubjectCode = " CODE ",
            SubjectName = " NAME ",
            Description = "   "
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("CODE", result.Data!.SubjectCode);
        Assert.Equal("NAME", result.Data.SubjectName);
        Assert.Null(result.Data.Description);

        var saved = await _dbContext.Subjects.IgnoreQueryFilters().FirstAsync();
        Assert.Equal("CODE", saved.SubjectCode);
        Assert.Equal("NAME", saved.SubjectName);
        Assert.Null(saved.Description);
    }

    [Fact]
    public async Task AuditFields_AreSetCorrectly()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var fixedTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var mockTime = new Mock<TimeProvider>();
        mockTime.Setup(x => x.GetUtcNow()).Returns(fixedTime);

        var sutWithFakeTime = new CreateSubjectUseCase(_dbContext, _tenantMock.Object, mockTime.Object);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var result = await sutWithFakeTime.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
            Assert.True(result.IsSuccess, $"Failed with {result.ErrorCode}");

            var saved = await _dbContext.Subjects.IgnoreQueryFilters().FirstOrDefaultAsync();
            Assert.NotNull(saved);

            Assert.Equal(centerId, saved.CenterId);
            Assert.True(saved.IsActive);
            Assert.False(saved.IsDeleted);
            Assert.Equal(1ul, saved.RowVersion);
            Assert.Equal("1", result.Data!.RowVersion);
            Assert.Equal(fixedTime.UtcDateTime, saved.CreatedAt);
            Assert.Equal(fixedTime.UtcDateTime, saved.UpdatedAt);
            Assert.Equal(userId, saved.CreatedBy);
            Assert.Equal(userId, saved.UpdatedBy);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task SameTenantDuplicate_ReturnsDuplicateResource()
    {
        var centerId = Guid.NewGuid();
        await SetupActiveCenter(centerId);
        _dbContext.Subjects.Add(new Subject { SubjectId = Guid.NewGuid(), CenterId = centerId, SubjectCode = "A", SubjectName = "A", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = " A ", SubjectName = "A" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task SameTenantDuplicate_ExactMatch_ReturnsDuplicateResource()
    {
        var centerId = Guid.NewGuid();
        await SetupActiveCenter(centerId);
        _dbContext.Subjects.Add(new Subject { SubjectId = Guid.NewGuid(), CenterId = centerId, SubjectCode = "A", SubjectName = "A", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });

        Assert.False(result.IsSuccess, "Expected DuplicateResource but got Success");
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantDuplicate_Success()
    {
        var center1 = Guid.NewGuid();
        var center2 = Guid.NewGuid();
        await SetupActiveCenter(center1);
        await SetupActiveCenter(center2);

        _dbContext.Subjects.Add(new Subject { SubjectId = Guid.NewGuid(), CenterId = center1, SubjectCode = "A", SubjectName = "A", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(center2);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UniqueRaceException_ReturnsDuplicateResource()
    {
        var centerId = Guid.NewGuid();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        tenantIdAccessorMock.SetupGet(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        var mockDb = new Mock<EduTwinDbContext>(options, tenantIdAccessorMock.Object) { CallBase = true };

        mockDb.Object.Centers.Add(new Center
        {
            CenterId = centerId, CenterCode = "C01", CenterName = "Test", Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active, IsDeleted = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        mockDb.Object.SaveChanges();

        mockDb.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("error", new Exception("ux_subjects_center_id_subject_code")));

        var sut = new CreateSubjectUseCase(mockDb.Object, _tenantMock.Object, _timeProvider);

        var result = await sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task UniqueRaceException_DeepNested_ReturnsDuplicateResource()
    {
        var centerId = Guid.NewGuid();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        tenantIdAccessorMock.SetupGet(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        var mockDb = new Mock<EduTwinDbContext>(options, tenantIdAccessorMock.Object) { CallBase = true };

        mockDb.Object.Centers.Add(new Center
        {
            CenterId = centerId, CenterCode = "C01", CenterName = "Test", Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active, IsDeleted = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        mockDb.Object.SaveChanges();

        mockDb.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("error", new Exception("wrapper", new Exception("ux_subjects_center_id_subject_code"))));

        var sut = new CreateSubjectUseCase(mockDb.Object, _tenantMock.Object, _timeProvider);

        var result = await sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task UnrelatedDbUpdateException_Throws()
    {
        var centerId = Guid.NewGuid();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        tenantIdAccessorMock.SetupGet(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        var mockDb = new Mock<EduTwinDbContext>(options, tenantIdAccessorMock.Object) { CallBase = true };

        mockDb.Object.Centers.Add(new Center
        {
            CenterId = centerId, CenterCode = "C01", CenterName = "Test", Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active, IsDeleted = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        mockDb.Object.SaveChanges();

        mockDb.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("error", new Exception("unrelated_constraint")));

        var sut = new CreateSubjectUseCase(mockDb.Object, _tenantMock.Object, _timeProvider);

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" }));
    }
    [Fact]
    public async Task InvalidPaths_DoNotPersist()
    {
        var centerId = Guid.NewGuid();
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        await _sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "", SubjectName = "A" });

        Assert.Empty(await _dbContext.Subjects.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task ExactCancellationToken_IsPassedToSaveChangesAsync()
    {
        var centerId = Guid.NewGuid();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        tenantIdAccessorMock.SetupGet(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        var mockDb = new Mock<EduTwinDbContext>(options, tenantIdAccessorMock.Object) { CallBase = true };

        mockDb.Object.Centers.Add(new Center
        {
            CenterId = centerId, CenterCode = "C01", CenterName = "Test", Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active, IsDeleted = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        mockDb.Object.SaveChanges();

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        mockDb.Setup(c => c.SaveChangesAsync(token)).ReturnsAsync(1).Verifiable();

        var sut = new CreateSubjectUseCase(mockDb.Object, _tenantMock.Object, _timeProvider);

        await sut.ExecuteAsync(new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" }, token);

        mockDb.Verify(c => c.SaveChangesAsync(token), Times.Once);
    }
}
