using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAdminSupportAndCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role_name",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_roles_account_type",
                table: "user_roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_account_type",
                table: "roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_permissions_account_type",
                table: "role_permissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_permission_account_types_account_type",
                table: "permission_account_types");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "a39a542b-7b4f-55f6-aa8d-4f6f2073f852", "manage", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép manage centers trong phạm vi được cấp.", false, true, "Platform", "platform.centers.manage", "Centers", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "d49095fa-30ce-5167-885e-489b19188769", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read centers trong phạm vi được cấp.", false, true, "Platform", "platform.centers.read", "Centers", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "faa3342c-5473-521b-a321-eb3844f638fb", "manage", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép manage managers trong phạm vi được cấp.", false, true, "Platform", "platform.managers.manage", "Managers", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "PlatformAdmin", "a39a542b-7b4f-55f6-aa8d-4f6f2073f852", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "d49095fa-30ce-5167-885e-489b19188769", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "faa3342c-5473-521b-a321-eb3844f638fb", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role_name",
                table: "users",
                sql: "role_name IN ('Student', 'Teacher', 'CenterManager', 'PlatformAdmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_roles_account_type",
                table: "user_roles",
                sql: "`account_type` IN ('Student', 'Teacher', 'CenterManager', 'PlatformAdmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_account_type",
                table: "roles",
                sql: "`account_type` IN ('Student', 'Teacher', 'CenterManager', 'PlatformAdmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_permissions_account_type",
                table: "role_permissions",
                sql: "`account_type` IN ('Student', 'Teacher', 'CenterManager', 'PlatformAdmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_permission_account_types_account_type",
                table: "permission_account_types",
                sql: "`account_type` IN ('Student', 'Teacher', 'CenterManager', 'PlatformAdmin')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_role_name",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_roles_account_type",
                table: "user_roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_account_type",
                table: "roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_permissions_account_type",
                table: "role_permissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_permission_account_types_account_type",
                table: "permission_account_types");

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "a39a542b-7b4f-55f6-aa8d-4f6f2073f852" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "d49095fa-30ce-5167-885e-489b19188769" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "faa3342c-5473-521b-a321-eb3844f638fb" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "a39a542b-7b4f-55f6-aa8d-4f6f2073f852");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "d49095fa-30ce-5167-885e-489b19188769");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "faa3342c-5473-521b-a321-eb3844f638fb");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_role_name",
                table: "users",
                sql: "role_name IN ('Student', 'Teacher', 'CenterManager')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_roles_account_type",
                table: "user_roles",
                sql: "`account_type` IN ('Student', 'Teacher', 'CenterManager')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_account_type",
                table: "roles",
                sql: "`account_type` IN ('Student', 'Teacher', 'CenterManager')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_permissions_account_type",
                table: "role_permissions",
                sql: "`account_type` IN ('Student', 'Teacher', 'CenterManager')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_permission_account_types_account_type",
                table: "permission_account_types",
                sql: "`account_type` IN ('Student', 'Teacher', 'CenterManager')");
        }
    }
}
