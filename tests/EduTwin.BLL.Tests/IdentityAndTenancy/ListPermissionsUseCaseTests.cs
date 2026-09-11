using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class ListPermissionsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsDeterministicCatalogProjection()
    {
        var fixture = await CreateFixtureAsync();
        var result = await fixture.Sut.ExecuteAsync(new PermissionListQuery
        {
            PageSize = 100
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(63, result.TotalItems);
        Assert.Equal(63, result.Data.Count);
        Assert.All(result.Data, permission =>
        {
            Assert.Equal(
                permission.PermissionCode.Split('.')[^1],
                permission.Action);
            Assert.NotEmpty(permission.AllowedAccountTypes);
            Assert.Equal(
                permission.AllowedAccountTypes
                    .OrderBy(value => Array.IndexOf(
                        [
                            nameof(UserRole.Student),
                            nameof(UserRole.Teacher),
                            nameof(UserRole.CenterManager)
                        ],
                        value)),
                permission.AllowedAccountTypes);
        });
        Assert.Equal(
            result.Data.Select(item => item.PermissionCode).Distinct().Count(),
            result.Data.Count);
        await fixture.Context.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_FiltersByModuleAccountTypeAndStatus()
    {
        var fixture = await CreateFixtureAsync();
        var result = await fixture.Sut.ExecuteAsync(new PermissionListQuery
        {
            Module = "Authorization",
            AccountType = UserRole.CenterManager,
            Status = PermissionStatus.Active,
            PageSize = 100
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.TotalItems);
        Assert.All(result.Data, permission =>
        {
            Assert.Equal("Authorization", permission.Module);
            Assert.Contains(nameof(UserRole.CenterManager), permission.AllowedAccountTypes);
            Assert.Equal(nameof(PermissionStatus.Active), permission.Status);
        });
        await fixture.Context.DisposeAsync();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ExecuteAsync_InvalidPagination_ReturnsValidationFailed(
        int page,
        int pageSize)
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Sut.ExecuteAsync(new PermissionListQuery
        {
            Page = page,
            PageSize = pageSize
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        await fixture.Context.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedTenant_FailsClosed()
    {
        var fixture = await CreateFixtureAsync(isResolved: false);

        var result = await fixture.Sut.ExecuteAsync(new PermissionListQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        await fixture.Context.DisposeAsync();
    }

    private static async Task<Fixture> CreateFixtureAsync(bool isResolved = true)
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(item => item.IsResolved).Returns(isResolved);
        tenantContext.SetupGet(item => item.CenterId).Returns(centerId);
        tenantContext.SetupGet(item => item.UserId).Returns(userId);
        var accessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        accessor.SetupGet(item => item.CenterId).Returns(centerId);
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new EduTwinDbContext(options, accessor.Object);
        context.Permissions.AddRange(
            AuthorizationPermissionCatalog.CreatePermissions());
        context.PermissionAccountTypes.AddRange(
            AuthorizationPermissionCatalog.CreateAccountTypeMappings());
        await context.SaveChangesAsync();

        return new Fixture(
            context,
            new ListPermissionsUseCase(context, tenantContext.Object));
    }

    private sealed record Fixture(
        EduTwinDbContext Context,
        ListPermissionsUseCase Sut);
}
