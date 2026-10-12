using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace EduTwin.BLL.Tests.Platform;

public sealed class AuthorizationMigrationSqlTests
{
    private static string Sql(DeleteDataOperation operation, bool explicitTypes = true)
    {
        if (explicitTypes) operation.KeyColumnTypes = Enumerable.Repeat("varchar(64)",operation.KeyColumns.Length).ToArray();
        using var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL("Server=127.0.0.1;Database=synthetic;User=synthetic;Password=unused;").Options);
        var generator = db.GetService<IMigrationsSqlGenerator>();
        Assert.IsType<AuthorizationMigrationsSqlGenerator>(generator);
        return string.Join("\n", generator.Generate([operation]).Select(c => c.CommandText));
    }

    [Fact]
    public void ApplicabilityDeletion_RemovesOnlyMatchingGrantsBeforeParent_RegardlessOfKeyOrder()
    {
        var sql = Sql(new() { Table="permission_account_types", KeyColumns=["account_type","permission_id"],
            KeyValues=new object[,] {{"Teacher","id-one"},{"CenterManager","id-two"}} });
        Assert.True(sql.IndexOf("DELETE FROM `role_permissions`",StringComparison.Ordinal)<sql.IndexOf("DELETE FROM `permission_account_types`",StringComparison.Ordinal));
        Assert.Contains("`permission_id` = 'id-one' AND `account_type` = 'Teacher'",sql);
        Assert.Contains("`permission_id` = 'id-two' AND `account_type` = 'CenterManager'",sql);
        Assert.DoesNotContain("DELETE FROM `users`",sql);
    }

    [Fact]
    public void OtherSeedTables_DoNotDeletePermissionGrants()
        => Assert.DoesNotContain("role_permissions",Sql(new(){Table="permissions",KeyColumns=["permission_id"],KeyValues=new object[,]{{"id-one"}}}));

    [Fact]
    public void MissingCompositeKey_DoesNotInventAnUnscopedGrantDeletion()
        => Assert.DoesNotContain("role_permissions",Sql(new(){Table="permission_account_types",KeyColumns=["permission_id"],KeyValues=new object[,]{{"id-one"}}}));

    [Fact]
    public void HistoricalTargetWithoutSeedEntities_UsesExplicitKeyTypes()
    {
        Assert.Contains("DELETE FROM `permission_account_types`",Sql(new(){Table="permission_account_types",KeyColumns=["account_type","permission_id"],KeyValues=new object[,]{{"Teacher","id-one"}}},false));
        Assert.Contains("DELETE FROM `permissions`",Sql(new(){Table="permissions",KeyColumns=["permission_id"],KeyValues=new object[,]{{"id-one"}}},false));
    }
}
