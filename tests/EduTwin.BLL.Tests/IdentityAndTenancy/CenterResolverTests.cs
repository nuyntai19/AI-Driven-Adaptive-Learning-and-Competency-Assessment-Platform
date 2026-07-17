using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Organization;
using EduTwin.BLL.Tests.Seeding; // to access TestAsyncQueryProvider and TestAsyncEnumerator

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class CenterResolverTests
{
    private static Mock<DbSet<T>> MockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));
        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
        return mockSet;
    }

    [Fact]
    public async Task BlankCenterCode_RejectedByCenterResolver()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().Options;
        var mockContext = new Mock<EduTwinDbContext>(options);
        mockContext.Setup(c => c.Set<Center>()).Returns(MockDbSet(new List<Center>()).Object);
        var resolver = new CenterResolver(mockContext.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => resolver.ResolveByCodeAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => resolver.ResolveByCodeAsync(null!));
    }
    [Fact]
    public async Task ExistingCenterCode_ReturnsProjectedCenter()
    {
        var centerId = Guid.NewGuid();
        var centers = new List<Center>
        {
            new Center { CenterId = centerId, CenterCode = "VALID_CODE", Status = EduTwin.Contracts.Organization.CenterStatus.Active }
        };

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().Options;
        var mockContext = new Mock<EduTwinDbContext>(options);
        mockContext.Setup(c => c.Set<Center>()).Returns(MockDbSet(centers).Object);
        var resolver = new CenterResolver(mockContext.Object);

        var result = await resolver.ResolveByCodeAsync("VALID_CODE");

        Assert.NotNull(result);
        Assert.Equal(centerId, result.CenterId);
        Assert.Equal("VALID_CODE", result.CenterCode);
        Assert.Equal(EduTwin.Contracts.Organization.CenterStatus.Active, result.Status);
    }

    [Fact]
    public async Task UnknownCenterCode_ReturnsNull()
    {
        var centers = new List<Center>
        {
            new Center { CenterId = Guid.NewGuid(), CenterCode = "VALID_CODE", Status = EduTwin.Contracts.Organization.CenterStatus.Active }
        };

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().Options;
        var mockContext = new Mock<EduTwinDbContext>(options);
        mockContext.Setup(c => c.Set<Center>()).Returns(MockDbSet(centers).Object);
        var resolver = new CenterResolver(mockContext.Object);

        var result = await resolver.ResolveByCodeAsync("UNKNOWN");

        Assert.Null(result);
    }

    [Fact]
    public async Task ExistingCenterProjection_ContainsIdCodeAndStatus()
    {
        var centerId = Guid.NewGuid();
        var centers = new List<Center>
        {
            new Center { CenterId = centerId, CenterCode = "PROJECTION", Status = EduTwin.Contracts.Organization.CenterStatus.Suspended }
        };

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().Options;
        var mockContext = new Mock<EduTwinDbContext>(options);
        mockContext.Setup(c => c.Set<Center>()).Returns(MockDbSet(centers).Object);
        var resolver = new CenterResolver(mockContext.Object);

        var result = await resolver.ResolveByCodeAsync("PROJECTION");

        Assert.NotNull(result);
        Assert.Equal(centerId, result.CenterId);
        Assert.Equal("PROJECTION", result.CenterCode);
        Assert.Equal(EduTwin.Contracts.Organization.CenterStatus.Suspended, result.Status);
    }
}
