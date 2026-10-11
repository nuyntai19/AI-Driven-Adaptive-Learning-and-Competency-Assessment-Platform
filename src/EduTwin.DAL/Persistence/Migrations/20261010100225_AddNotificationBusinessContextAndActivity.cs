using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationBusinessContextAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "resource_context",
                table: "notification_events",
                type: "json",
                nullable: true);

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "1e9609fe-808d-590e-8590-b20fddea0b74", "read_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_own history trong phạm vi được cấp.", true, false, "Activity", "activity.history.read_own", "History", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "43abd60e-32fb-5377-95cd-4c989a84ce50", "read_administrative", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_administrative history trong phạm vi được cấp.", true, false, "Activity", "activity.history.read_administrative", "History", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "b296b3ad-cbf9-584f-bc2a-2cfeffc6e66b", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read activity trong phạm vi được cấp.", false, false, "Platform", "platform.activity.read", "Activity", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "c8fae52e-b814-5d49-9218-4e1ed1099454", "read_scoped", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_scoped history trong phạm vi được cấp.", true, false, "Activity", "activity.history.read_scoped", "History", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "Student", "1e9609fe-808d-590e-8590-b20fddea0b74", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "43abd60e-32fb-5377-95cd-4c989a84ce50", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "b296b3ad-cbf9-584f-bc2a-2cfeffc6e66b", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "c8fae52e-b814-5d49-9218-4e1ed1099454", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM role_permissions WHERE permission_id IN ('1e9609fe-808d-590e-8590-b20fddea0b74','43abd60e-32fb-5377-95cd-4c989a84ce50','b296b3ad-cbf9-584f-bc2a-2cfeffc6e66b','c8fae52e-b814-5d49-9218-4e1ed1099454')");
            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Student", "1e9609fe-808d-590e-8590-b20fddea0b74" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "43abd60e-32fb-5377-95cd-4c989a84ce50" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "b296b3ad-cbf9-584f-bc2a-2cfeffc6e66b" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "c8fae52e-b814-5d49-9218-4e1ed1099454" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "1e9609fe-808d-590e-8590-b20fddea0b74");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "43abd60e-32fb-5377-95cd-4c989a84ce50");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "b296b3ad-cbf9-584f-bc2a-2cfeffc6e66b");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "c8fae52e-b814-5d49-9218-4e1ed1099454");

            migrationBuilder.DropColumn(
                name: "resource_context",
                table: "notification_events");
        }
    }
}
