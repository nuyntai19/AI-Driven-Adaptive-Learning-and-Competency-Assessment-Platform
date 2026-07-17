using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.BLL.Seeding;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class GlobalQueryFilterTests
{
    private EduTwinDbContext CreateContext(ITenantIdAccessor? accessor = null)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL("Server=dummy")
            .Options;

        return accessor == null ? new EduTwinDbContext(options) : new EduTwinDbContext(options, accessor);
    }

#pragma warning disable CS0618

    [Fact]
    public void EveryTenantOwnedEntity_HasQueryFilter()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().Where(e => typeof(ITenantOwnedEntity).IsAssignableFrom(e.ClrType)).ToList();

        Assert.Equal(30, entityTypes.Count); // Total 30 tenant-owned entities

        foreach (var entityType in entityTypes)
        {
            Assert.NotNull(entityType.GetQueryFilter());
        }
    }

    [Fact]
    public void EveryMutableTenantAggregate_FilterIncludesCenterAndSoftDelete()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().Where(e => typeof(IMutableTenantAggregate).IsAssignableFrom(e.ClrType)).ToList();

        Assert.Equal(19, entityTypes.Count); // 19 MTA

        foreach (var entityType in entityTypes)
        {
            var filter = entityType.GetQueryFilter();
            Assert.NotNull(filter);
            var filterString = filter.ToString();
            Assert.Contains("CurrentTenantId", filterString);
            Assert.Contains("IsDeleted", filterString);
        }
    }

    [Fact]
    public void EveryAppendOnlyEntity_FilterIncludesCenterOnly()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().Where(e => typeof(ITenantAppendOnlyEntity).IsAssignableFrom(e.ClrType)).ToList();

        Assert.Equal(5, entityTypes.Count); // 5 Append Only

        foreach (var entityType in entityTypes)
        {
            var filter = entityType.GetQueryFilter();
            Assert.NotNull(filter);
            var filterString = filter.ToString();
            Assert.Contains("CurrentTenantId", filterString);
            Assert.DoesNotContain("IsDeleted", filterString);
        }
    }

    [Fact]
    public void EveryTenantJoinEntity_FilterIncludesCenterOnly()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().Where(e => typeof(ITenantJoinEntity).IsAssignableFrom(e.ClrType)).ToList();

        Assert.Equal(6, entityTypes.Count); // 6 Join Entities

        foreach (var entityType in entityTypes)
        {
            var filter = entityType.GetQueryFilter();
            Assert.NotNull(filter);
            var filterString = filter.ToString();
            Assert.Contains("CurrentTenantId", filterString);
            Assert.DoesNotContain("IsDeleted", filterString);
        }
    }

    [Fact]
    public void CenterEntity_HasNoTenantQueryFilter()
    {
        using var context = CreateContext();
        var centerType = context.Model.FindEntityType(typeof(Center));

        Assert.NotNull(centerType);
        Assert.Null(centerType.GetQueryFilter());

        var sql = context.Centers.ToQueryString().ToLowerInvariant();
        Assert.DoesNotContain("where", sql);
    }

    [Fact]
    public void UnresolvedTenantContext_UsesFailClosedTenantId()
    {
        var context = new TenantContext();
        using var dbContext = CreateContext(context);

        var sql = dbContext.Users.ToQueryString().ToLowerInvariant();
        var emptyGuidStr = Guid.Empty.ToString().ToLowerInvariant();
        Assert.True(sql.Contains($"'{emptyGuidStr}'") || sql.Contains("00000000-0000-0000-0000-000000000000") || sql.Contains(Guid.Empty.ToString("D").ToLowerInvariant()));
    }

    [Fact]
    public void ResolvedTenantContext_UsesCurrentCenterId()
    {
        var context = new TenantContext();
        var centerId = Guid.NewGuid();
        context.Initialize(centerId, Guid.NewGuid(), "Teacher", 1);

        using var dbContext = CreateContext(context);

        var sql = dbContext.Users.ToQueryString().ToLowerInvariant();
        Assert.Contains(centerId.ToString("D").ToLowerInvariant(), sql);
    }

    [Fact]
    public void TwoDbContextsWithDifferentTenants_DoNotShareTenantParameter()
    {
        var centerId1 = Guid.NewGuid();
        var context1 = new TenantContext();
        context1.Initialize(centerId1, Guid.NewGuid(), "Teacher", 1);
        using var dbContext1 = CreateContext(context1);
        var sql1 = dbContext1.Users.ToQueryString().ToLowerInvariant();

        var centerId2 = Guid.NewGuid();
        var context2 = new TenantContext();
        context2.Initialize(centerId2, Guid.NewGuid(), "Teacher", 1);
        using var dbContext2 = CreateContext(context2);
        var sql2 = dbContext2.Users.ToQueryString().ToLowerInvariant();

        Assert.Contains(centerId1.ToString("D").ToLowerInvariant(), sql1);
        Assert.DoesNotContain(centerId2.ToString("D").ToLowerInvariant(), sql1);

        Assert.Contains(centerId2.ToString("D").ToLowerInvariant(), sql2);
        Assert.DoesNotContain(centerId1.ToString("D").ToLowerInvariant(), sql2);
    }

    [Fact]
    public void QueryFilterModel_IsReusableAcrossTenantScopes()
    {
        var centerId1 = Guid.NewGuid();
        var context1 = new TenantContext();
        context1.Initialize(centerId1, Guid.NewGuid(), "Teacher", 1);
        using var dbContext1 = CreateContext(context1);
        var sql1 = dbContext1.Users.ToQueryString().ToLowerInvariant();

        var centerId2 = Guid.NewGuid();
        var context2 = new TenantContext();
        context2.Initialize(centerId2, Guid.NewGuid(), "Teacher", 1);
        using var dbContext2 = CreateContext(context2);
        var sql2 = dbContext2.Users.ToQueryString().ToLowerInvariant();

        Assert.Same(dbContext1.Model, dbContext2.Model);
        Assert.NotEqual(sql1, sql2);
    }

    [Fact]
    public void TenantIdAccessor_InSameDIScopeSharesTenantContextInstance()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Server=dummy"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddEduTwinBll(configuration);
        services.AddIdentityAndTenancy();

        var serviceProvider = services.BuildServiceProvider();

        using var scope1 = serviceProvider.CreateScope();
        var sp1 = scope1.ServiceProvider;

        var init1 = sp1.GetRequiredService<ITenantContextInitializer>();
        var centerId1 = Guid.NewGuid();
        init1.Initialize(centerId1, Guid.NewGuid(), "Teacher", 1);

        var db1 = sp1.GetRequiredService<EduTwinDbContext>();
        Assert.Equal(centerId1, db1.CurrentTenantId);

        using var scope2 = serviceProvider.CreateScope();
        var sp2 = scope2.ServiceProvider;
        var init2 = sp2.GetRequiredService<ITenantContextInitializer>();
        var centerId2 = Guid.NewGuid();
        init2.Initialize(centerId2, Guid.NewGuid(), "Teacher", 1);

        var db2 = sp2.GetRequiredService<EduTwinDbContext>();
        Assert.Equal(centerId2, db2.CurrentTenantId);

        using var scope3 = serviceProvider.CreateScope();
        var sp3 = scope3.ServiceProvider;
        var db3 = sp3.GetRequiredService<EduTwinDbContext>();
        Assert.Equal(Guid.Empty, db3.CurrentTenantId);

        Assert.NotSame(db1, db2);
        Assert.NotSame(db2, db3);
    }

    [Fact]
    public void OptionsOnlyDbContext_IsFailClosed()
    {
        using var dbContext = CreateContext(); // Uses parameterless accessor
        Assert.Equal(Guid.Empty, dbContext.CurrentTenantId);
    }

    [Fact]
    public void NoBusinessCodeUsesIgnoreQueryFilters()
    {
        var basePath = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(basePath);
        while (dir != null && dir.GetFiles("EduTwin.sln").Length == 0)
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);

        var srcDir = Path.Combine(dir.FullName, "src");
        var allCsFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);

        var filesWithBypass = allCsFiles
            .Where(file => File.ReadAllText(file).Contains("IgnoreQueryFilters"))
            .Select(file => Path.GetRelativePath(dir.FullName, file).Replace("\\", "/"))
            .ToList();

        Assert.Single(filesWithBypass);
        Assert.Equal("src/EduTwin.BLL/Seeding/ManifestEvaluator.cs", filesWithBypass[0]);
    }
}
