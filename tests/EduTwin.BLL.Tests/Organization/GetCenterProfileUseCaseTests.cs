using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Organization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.IdentityAndTenancy;
using System.Globalization;

namespace EduTwin.BLL.Tests.Organization;

public class GetCenterProfileUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly GetCenterProfileUseCase _sut;
    private readonly Guid _centerId = Guid.NewGuid();

    public GetCenterProfileUseCaseTests()
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

        _sut = new GetCenterProfileUseCase(_dbContext, _mockTenantContext.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetCenterProfile_CenterManager_ReturnsContractData()
    {
        // Arrange
        var center = new Center
        {
            CenterId = _centerId,
            CenterCode = "EDU-TEST",
            CenterName = "Test Center",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            RowVersion = 123456,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Centers.Add(center);
        await _dbContext.SaveChangesAsync();
        var expectedRowVersion = center.RowVersion.ToString(CultureInfo.InvariantCulture);
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _sut.ExecuteAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(_centerId.ToString("D").ToLowerInvariant(), result.CenterId);
        Assert.Equal("EDU-TEST", result.CenterCode);
        Assert.Equal("Test Center", result.CenterName);
        Assert.Equal(nameof(CenterStatus.Active), result.Status);
        Assert.Equal("Asia/Ho_Chi_Minh", result.Timezone);
        Assert.Equal(expectedRowVersion, result.RowVersion);

        // Entity should not be tracked / modified
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetCenterProfile_UnresolvedTenant_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.IsResolved).Returns(false);
        var result = await _sut.ExecuteAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCenterProfile_MissingCenterId_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns((Guid?)null);
        var result = await _sut.ExecuteAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCenterProfile_EmptyCenterId_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns(Guid.Empty);
        var result = await _sut.ExecuteAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCenterProfile_MissingCenter_ResourceNotFound()
    {
        // DB is empty
        var result = await _sut.ExecuteAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCenterProfile_SoftDeletedCenter_ResourceNotFound()
    {
        var center = new Center
        {
            CenterId = _centerId,
            CenterCode = "EDU-TEST",
            CenterName = "Test Center",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Centers.Add(center);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCenterProfile_DoesNotReturnAnotherTenantCenter()
    {
        var anotherCenterId = Guid.NewGuid();
        var center = new Center
        {
            CenterId = anotherCenterId,
            CenterCode = "EDU-OTHER",
            CenterName = "Other Center",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Centers.Add(center);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCenterProfile_NullRole_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns((string?)null);
        var result = await _sut.ExecuteAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCenterProfile_EmptyRole_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns("");
        var result = await _sut.ExecuteAsync();
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
    public async Task GetCenterProfile_InvalidRole_ResourceNotFound(string role)
    {
        _mockTenantContext.Setup(c => c.Role).Returns(role);
        var result = await _sut.ExecuteAsync();
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public void OrganizationDependencyInjection_ResolvesGetCenterProfileUseCase()
    {
        var services = new ServiceCollection();

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase("DI").Options;
        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        services.AddSingleton(new EduTwinDbContext(options, mockAccessor.Object));

        services.AddSingleton(new Mock<ITenantContext>().Object);

        services.AddOrganization();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IGetCenterProfileUseCase>();
        Assert.NotNull(resolved);
        Assert.IsType<GetCenterProfileUseCase>(resolved);
    }
}
