using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAuthorizationPermissionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "16a89dc4-b201-5035-b923-e28c9bbacb52", "update_scoped", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update_scoped student trong phạm vi được cấp.", true, true, "Twin", "twin.student.update_scoped", "Student", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "6d8bae5b-307a-5534-a79d-2c253645dbfc", "delete", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép delete questions trong phạm vi được cấp.", true, true, "Curriculum", "curriculum.questions.delete", "Questions", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "af2001eb-2f1b-596c-8de0-1e6a1fca6047", "update_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update_own student trong phạm vi được cấp.", true, false, "Twin", "twin.student.update_own", "Student", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "CenterManager", "16a89dc4-b201-5035-b923-e28c9bbacb52", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "16a89dc4-b201-5035-b923-e28c9bbacb52", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "6d8bae5b-307a-5534-a79d-2c253645dbfc", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "af2001eb-2f1b-596c-8de0-1e6a1fca6047", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM role_permissions
                WHERE permission_id IN (
                    '16a89dc4-b201-5035-b923-e28c9bbacb52',
                    '6d8bae5b-307a-5534-a79d-2c253645dbfc',
                    'af2001eb-2f1b-596c-8de0-1e6a1fca6047');
                """);

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "16a89dc4-b201-5035-b923-e28c9bbacb52" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "16a89dc4-b201-5035-b923-e28c9bbacb52" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "6d8bae5b-307a-5534-a79d-2c253645dbfc" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Student", "af2001eb-2f1b-596c-8de0-1e6a1fca6047" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "16a89dc4-b201-5035-b923-e28c9bbacb52");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "6d8bae5b-307a-5534-a79d-2c253645dbfc");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "af2001eb-2f1b-596c-8de0-1e6a1fca6047");
        }
    }
}
