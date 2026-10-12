using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "2f69bf58-c875-57fe-8109-0b4d4ae1400b", "redrive", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép redrive dispatches trong phạm vi được cấp.", true, true, "Notifications", "notifications.dispatches.redrive", "Dispatches", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "5ce360b7-5e8d-5667-97b1-83ebfeeeafe1", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read dispatches trong phạm vi được cấp.", true, false, "Notifications", "notifications.dispatches.read", "Dispatches", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "8fd77445-dcb9-55bc-81a9-892074f4a6db", "redrive", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép redrive notifications trong phạm vi được cấp.", false, true, "Platform", "platform.notifications.redrive", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "a1d4984e-8a71-57e7-92c8-0934529bcc5e", "read_dispatches", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_dispatches notifications trong phạm vi được cấp.", false, false, "Platform", "platform.notifications.read_dispatches", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "CenterManager", "2f69bf58-c875-57fe-8109-0b4d4ae1400b", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "5ce360b7-5e8d-5667-97b1-83ebfeeeafe1", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "8fd77445-dcb9-55bc-81a9-892074f4a6db", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "a1d4984e-8a71-57e7-92c8-0934529bcc5e", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM role_permissions WHERE permission_id IN ('2f69bf58-c875-57fe-8109-0b4d4ae1400b','5ce360b7-5e8d-5667-97b1-83ebfeeeafe1','8fd77445-dcb9-55bc-81a9-892074f4a6db','a1d4984e-8a71-57e7-92c8-0934529bcc5e')");
            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "2f69bf58-c875-57fe-8109-0b4d4ae1400b" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "5ce360b7-5e8d-5667-97b1-83ebfeeeafe1" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "8fd77445-dcb9-55bc-81a9-892074f4a6db" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "a1d4984e-8a71-57e7-92c8-0934529bcc5e" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "2f69bf58-c875-57fe-8109-0b4d4ae1400b");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "5ce360b7-5e8d-5667-97b1-83ebfeeeafe1");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "8fd77445-dcb9-55bc-81a9-892074f4a6db");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "a1d4984e-8a71-57e7-92c8-0934529bcc5e");
        }
    }
}
