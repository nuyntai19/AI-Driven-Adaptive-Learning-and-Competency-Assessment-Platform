using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformOperationalMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_ai_calls",
                columns: table => new
                {
                    call_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    provider = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    model = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    pool_alias = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    stage = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    outcome = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    input_tokens = table.Column<long>(type: "bigint", nullable: true),
                    output_tokens = table.Column<long>(type: "bigint", nullable: true),
                    thinking_tokens = table.Column<long>(type: "bigint", nullable: true),
                    cached_tokens = table.Column<long>(type: "bigint", nullable: true),
                    total_tokens = table.Column<long>(type: "bigint", nullable: true),
                    price_version = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true),
                    estimated_usd = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_ai_calls", x => x.call_id);
                    table.CheckConstraint("ck_platform_ai_call_duration", "duration_ms >= 0");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "platform_ops_alerts",
                columns: table => new
                {
                    alert_key = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    first_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    notified_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    episode_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_ops_alerts", x => x.alert_key);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "platform_ops_snapshots",
                columns: table => new
                {
                    snapshot_key = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    captured_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    payload = table.Column<string>(type: "json", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_ops_snapshots", x => x.snapshot_key);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "073c9a09-d4f2-50e8-99e7-2c070f82051b", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read storage trong phạm vi được cấp.", false, true, "Platform", "platform.storage.read", "Storage", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "44f72e24-4cf4-588e-a2a2-c08a4167e752", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read operations trong phạm vi được cấp.", false, true, "Platform", "platform.operations.read", "Operations", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "8a4fecaa-a573-51d0-9fa1-e79b55f67dc1", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read ai_usage trong phạm vi được cấp.", false, true, "Platform", "platform.ai_usage.read", "Ai_usage", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "PlatformAdmin", "073c9a09-d4f2-50e8-99e7-2c070f82051b", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "44f72e24-4cf4-588e-a2a2-c08a4167e752", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "8a4fecaa-a573-51d0-9fa1-e79b55f67dc1", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_ai_calls_started_at_provider_model",
                table: "platform_ai_calls",
                columns: new[] { "started_at", "provider", "model" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_ai_calls");

            migrationBuilder.DropTable(
                name: "platform_ops_alerts");

            migrationBuilder.DropTable(
                name: "platform_ops_snapshots");

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "073c9a09-d4f2-50e8-99e7-2c070f82051b" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "44f72e24-4cf4-588e-a2a2-c08a4167e752" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "8a4fecaa-a573-51d0-9fa1-e79b55f67dc1" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "073c9a09-d4f2-50e8-99e7-2c070f82051b");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "44f72e24-4cf4-588e-a2a2-c08a4167e752");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "8a4fecaa-a573-51d0-9fa1-e79b55f67dc1");
        }
    }
}
