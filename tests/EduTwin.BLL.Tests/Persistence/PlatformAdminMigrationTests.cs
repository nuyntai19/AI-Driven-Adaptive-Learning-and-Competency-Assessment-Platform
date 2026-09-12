using System.Linq;
using System.Reflection;
using Xunit;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using EduTwin.DAL.Persistence.Migrations;

namespace EduTwin.BLL.Tests.Persistence;

public class PlatformAdminMigrationTests
{
    [Fact]
    public void Migration_AddsAll5CheckConstraints_AcceptingPlatformAdmin()
    {
        var migration = new AddPlatformAdminSupportAndCheckConstraints();
        var migrationBuilder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        typeof(Migration).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(migration, [migrationBuilder]);

        var checkConstraints = migrationBuilder.Operations
            .OfType<AddCheckConstraintOperation>()
            .ToList();

        Assert.Equal(5, checkConstraints.Count);

        // 1. users: ck_users_role_name
        var userCheck = checkConstraints.FirstOrDefault(c => c.Name == "ck_users_role_name" && c.Table == "users");
        Assert.NotNull(userCheck);
        Assert.Contains("'PlatformAdmin'", userCheck.Sql);

        // 2. roles: ck_roles_account_type
        var roleCheck = checkConstraints.FirstOrDefault(c => c.Name == "ck_roles_account_type" && c.Table == "roles");
        Assert.NotNull(roleCheck);
        Assert.Contains("'PlatformAdmin'", roleCheck.Sql);

        // 3. permission_account_types: ck_permission_account_types_account_type
        var permCheck = checkConstraints.FirstOrDefault(c => c.Name == "ck_permission_account_types_account_type" && c.Table == "permission_account_types");
        Assert.NotNull(permCheck);
        Assert.Contains("'PlatformAdmin'", permCheck.Sql);

        // 4. user_roles: ck_user_roles_account_type
        var userRoleCheck = checkConstraints.FirstOrDefault(c => c.Name == "ck_user_roles_account_type" && c.Table == "user_roles");
        Assert.NotNull(userRoleCheck);
        Assert.Contains("'PlatformAdmin'", userRoleCheck.Sql);

        // 5. role_permissions: ck_role_permissions_account_type
        var rolePermCheck = checkConstraints.FirstOrDefault(c => c.Name == "ck_role_permissions_account_type" && c.Table == "role_permissions");
        Assert.NotNull(rolePermCheck);
        Assert.Contains("'PlatformAdmin'", rolePermCheck.Sql);
    }

    [Fact]
    public void Migration_SeedsPlatformPermissionsAndAccountTypeMappings()
    {
        var migration = new AddPlatformAdminSupportAndCheckConstraints();
        var migrationBuilder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        typeof(Migration).GetMethod("Up", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(migration, [migrationBuilder]);

        var insertOperations = migrationBuilder.Operations
            .OfType<InsertDataOperation>()
            .ToList();

        var permInsert = insertOperations.FirstOrDefault(o => o.Table == "permissions");
        Assert.NotNull(permInsert);
        var permValues = permInsert.Values;
        Assert.NotNull(permValues);

        var mappingInsert = insertOperations.FirstOrDefault(o => o.Table == "permission_account_types");
        Assert.NotNull(mappingInsert);
        var mappingValues = mappingInsert.Values;
        Assert.NotNull(mappingValues);
    }
}
