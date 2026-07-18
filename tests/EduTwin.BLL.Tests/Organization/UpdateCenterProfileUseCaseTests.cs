using System;
using System.Globalization;
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
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.Organization;

public class UpdateCenterProfileUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly UpdateCenterProfileUseCase _sut;
    private readonly Guid _centerId = Guid.NewGuid();
    private ulong _initialRowVersion;
    private DateTimeOffset _currentTime;

    public UpdateCenterProfileUseCaseTests()
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

        _mockTimeProvider = new Mock<TimeProvider>();
        _currentTime = DateTimeOffset.UtcNow;
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(() => _currentTime);

        _sut = new UpdateCenterProfileUseCase(_dbContext, _mockTenantContext.Object, _mockTimeProvider.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private async Task SeedCenterAsync()
    {
        var center = new Center
        {
            CenterId = _centerId,
            CenterCode = "EDU-TEST",
            CenterName = "Test Center",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = _currentTime.UtcDateTime.AddDays(-1),
            UpdatedAt = _currentTime.UtcDateTime.AddDays(-1)
        };
        _dbContext.Centers.Add(center);
        await _dbContext.SaveChangesAsync();
        _initialRowVersion = center.RowVersion;
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task UpdateCenterProfile_ValidRequest_UpdatesNameAndTimezone()
    {
        await SeedCenterAsync();
        var request = new UpdateCenterProfileRequest
        {
            CenterName = "New Name",
            Timezone = "UTC",
            RowVersion = _initialRowVersion.ToString(CultureInfo.InvariantCulture)
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.CenterName);
        Assert.Equal("UTC", result.Timezone);

        var persisted = await _dbContext.Centers.FindAsync(_centerId);
        Assert.NotNull(persisted);
        Assert.Equal("New Name", persisted.CenterName);
        Assert.Equal("UTC", persisted.Timezone);
    }

    [Fact]
    public async Task UpdateCenterProfile_ValidRequest_IncrementsRowVersion()
    {
        await SeedCenterAsync();
        var request = new UpdateCenterProfileRequest
        {
            CenterName = "New Name",
            Timezone = "UTC",
            RowVersion = _initialRowVersion.ToString(CultureInfo.InvariantCulture)
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);

        var persisted = await _dbContext.Centers.FindAsync(_centerId);
        Assert.NotNull(persisted);
        Assert.Equal(_initialRowVersion + 1, persisted.RowVersion);
        Assert.Equal(persisted.RowVersion.ToString(CultureInfo.InvariantCulture), result.RowVersion);
        Assert.Equal(_centerId.ToString("D").ToLowerInvariant(), result.CenterId);
    }

    [Fact]
    public async Task UpdateCenterProfile_DoesNotChangeCenterCodeOrStatus()
    {
        await SeedCenterAsync();
        var request = new UpdateCenterProfileRequest
        {
            CenterName = "New Name",
            Timezone = "UTC",
            RowVersion = _initialRowVersion.ToString(CultureInfo.InvariantCulture)
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("EDU-TEST", result.CenterCode);
        Assert.Equal(nameof(CenterStatus.Active), result.Status);

        var persisted = await _dbContext.Centers.FindAsync(_centerId);
        Assert.NotNull(persisted);
        Assert.Equal("EDU-TEST", persisted.CenterCode);
        Assert.Equal(CenterStatus.Active, persisted.Status);
    }

    [Fact]
    public async Task UpdateCenterProfile_UsesTimeProviderForUpdatedAt()
    {
        await SeedCenterAsync();
        var request = new UpdateCenterProfileRequest
        {
            CenterName = "New Name",
            Timezone = "UTC",
            RowVersion = _initialRowVersion.ToString(CultureInfo.InvariantCulture)
        };

        _currentTime = _currentTime.AddHours(1);
        var expectedTime = _currentTime.UtcDateTime;

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);

        var persisted = await _dbContext.Centers.FindAsync(_centerId);
        Assert.NotNull(persisted);
        Assert.Equal(expectedTime, persisted.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCenterProfile_StaleRowVersion_ConcurrencyConflict()
    {
        await SeedCenterAsync();
        var request = new UpdateCenterProfileRequest
        {
            CenterName = "New Name",
            Timezone = "UTC",
            RowVersion = (_initialRowVersion + 1).ToString(CultureInfo.InvariantCulture)
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_StaleRowVersion_DoesNotModifyEntity()
    {
        await SeedCenterAsync();
        var request = new UpdateCenterProfileRequest
        {
            CenterName = "New Name",
            Timezone = "UTC",
            RowVersion = (_initialRowVersion + 1).ToString(CultureInfo.InvariantCulture)
        };

        await _sut.ExecuteAsync(request);

        var persisted = await _dbContext.Centers.FindAsync(_centerId);
        Assert.NotNull(persisted);
        Assert.Equal("Test Center", persisted.CenterName);
        Assert.Equal("Asia/Ho_Chi_Minh", persisted.Timezone);
        Assert.Equal(_initialRowVersion, persisted.RowVersion);
    }

    [Fact]
    public async Task UpdateCenterProfile_UnresolvedTenant_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.IsResolved).Returns(false);
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_MissingCenterId_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns((Guid?)null);
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_EmptyCenterId_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns(Guid.Empty);
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_MissingCenter_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest
        {
            CenterName = "N", Timezone = "T", RowVersion = "1"
        });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_SoftDeletedCenter_ResourceNotFound()
    {
        await SeedCenterAsync();
        var center = await _dbContext.Centers.FindAsync(_centerId);
        center!.IsDeleted = true;
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest
        {
            CenterName = "N", Timezone = "T", RowVersion = center.RowVersion.ToString(CultureInfo.InvariantCulture)
        });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_CrossTenant_ResourceNotFound()
    {
        await SeedCenterAsync();
        _mockTenantContext.Setup(c => c.CenterId).Returns(Guid.NewGuid());
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest
        {
            CenterName = "N", Timezone = "T", RowVersion = _initialRowVersion.ToString(CultureInfo.InvariantCulture)
        });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Admin")]
    [InlineData("centermanager")]
    [InlineData("Center Manager")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateCenterProfile_InvalidRole_ResourceNotFound(string? role)
    {
        _mockTenantContext.Setup(c => c.Role).Returns(role);
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateCenterProfile_BlankCenterName_ValidationFailed(string centerName)
    {
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest { CenterName = centerName, Timezone = "T", RowVersion = "1" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_CenterNameTooLong_ValidationFailed()
    {
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest { CenterName = new string('A', 201), Timezone = "T", RowVersion = "1" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateCenterProfile_BlankTimezone_ValidationFailed(string timezone)
    {
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest { CenterName = "N", Timezone = timezone, RowVersion = "1" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_TimezoneTooLong_ValidationFailed()
    {
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest { CenterName = "N", Timezone = new string('A', 65), RowVersion = "1" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData(" 1 ")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("18446744073709551616")] // ulong.MaxValue + 1
    public async Task UpdateCenterProfile_InvalidRowVersion_ValidationFailed(string? rowVersion)
    {
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest { CenterName = "N", Timezone = "T", RowVersion = rowVersion! });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateCenterProfile_ZeroRowVersion_ValidationFailed()
    {
        var result = await _sut.ExecuteAsync(new UpdateCenterProfileRequest { CenterName = "N", Timezone = "T", RowVersion = "0" });
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public void OrganizationDependencyInjection_ResolvesUpdateCenterProfileUseCase()
    {
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase("DI").Options;
        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        services.AddSingleton(new EduTwinDbContext(options, mockAccessor.Object));
        services.AddSingleton(new Mock<ITenantContext>().Object);
        services.AddSingleton<TimeProvider>(Mock.Of<TimeProvider>());
        services.AddOrganization();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IUpdateCenterProfileUseCase>();
        Assert.NotNull(resolved);
        Assert.IsType<UpdateCenterProfileUseCase>(resolved);
    }
}
