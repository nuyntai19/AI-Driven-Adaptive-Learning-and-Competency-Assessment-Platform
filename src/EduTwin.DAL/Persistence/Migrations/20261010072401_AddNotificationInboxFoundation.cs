using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationInboxFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_events",
                columns: table => new
                {
                    event_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    deduplication_key = table.Column<string>(type: "varchar(160)", nullable: false),
                    event_type = table.Column<string>(type: "varchar(64)", nullable: false),
                    title = table.Column<string>(type: "varchar(160)", nullable: false),
                    body = table.Column<string>(type: "varchar(2000)", nullable: false),
                    recipient_user_ids = table.Column<string>(type: "json", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_events", x => x.event_id);
                    table.UniqueConstraint("AK_notification_events_center_id_event_id", x => new { x.center_id, x.event_id });
                    table.ForeignKey(
                        name: "FK_notification_events_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_events_users_center_id_created_by",
                        columns: x => new { x.center_id, x.created_by },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    delivery_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    event_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    user_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    read_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.delivery_id);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_notification_events_center_id_event_~",
                        columns: x => new { x.center_id, x.event_id },
                        principalTable: "notification_events",
                        principalColumns: new[] { "center_id", "event_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_users_center_id_user_id",
                        columns: x => new { x.center_id, x.user_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notification_dispatches",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    event_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    state = table.Column<string>(type: "varchar(32)", nullable: false),
                    available_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    lease_token = table.Column<string>(type: "varchar(36)", nullable: true),
                    lease_until = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    attempt_count = table.Column<int>(type: "int", nullable: false),
                    delivered_count = table.Column<int>(type: "int", nullable: false),
                    suppressed_count = table.Column<int>(type: "int", nullable: false),
                    last_error_code = table.Column<string>(type: "varchar(64)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_dispatches", x => new { x.center_id, x.event_id });
                    table.CheckConstraint("ck_notification_dispatch_state", "`state` IN ('Pending','Processing','Completed','DeadLetter')");
                    table.ForeignKey(
                        name: "FK_notification_dispatches_notification_events_center_id_event_~",
                        columns: x => new { x.center_id, x.event_id },
                        principalTable: "notification_events",
                        principalColumns: new[] { "center_id", "event_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "454c6647-6007-52be-8ab4-76fb15d58a9a", "read_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_own inbox trong phạm vi được cấp.", true, false, "Notifications", "notifications.inbox.read_own", "Inbox", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "8d5472e5-47d3-5d9a-afca-e744135687e7", "read_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_own notifications trong phạm vi được cấp.", false, false, "Platform", "platform.notifications.read_own", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "b0a8d388-56fe-5dbe-8f84-f6dedb768bfc", "update_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update_own notifications trong phạm vi được cấp.", false, false, "Platform", "platform.notifications.update_own", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "b6ebd012-bfe1-5e03-884c-43aafd32f1b8", "update_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update_own inbox trong phạm vi được cấp.", true, false, "Notifications", "notifications.inbox.update_own", "Inbox", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "CenterManager", "454c6647-6007-52be-8ab4-76fb15d58a9a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "454c6647-6007-52be-8ab4-76fb15d58a9a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "454c6647-6007-52be-8ab4-76fb15d58a9a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "8d5472e5-47d3-5d9a-afca-e744135687e7", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "b0a8d388-56fe-5dbe-8f84-f6dedb768bfc", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "b6ebd012-bfe1-5e03-884c-43aafd32f1b8", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "b6ebd012-bfe1-5e03-884c-43aafd32f1b8", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "b6ebd012-bfe1-5e03-884c-43aafd32f1b8", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_center_id_event_id_user_id",
                table: "notification_deliveries",
                columns: new[] { "center_id", "event_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_center_id_user_id_read_at_delivery_id",
                table: "notification_deliveries",
                columns: new[] { "center_id", "user_id", "read_at", "delivery_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_dispatches_state_available_at_center_id",
                table: "notification_dispatches",
                columns: new[] { "state", "available_at", "center_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_events_center_id_created_by",
                table: "notification_events",
                columns: new[] { "center_id", "created_by" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_events_center_id_deduplication_key",
                table: "notification_events",
                columns: new[] { "center_id", "deduplication_key" },
                unique: true);

            migrationBuilder.Sql("CREATE TRIGGER trg_notification_events_no_update BEFORE UPDATE ON notification_events FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Notification events are immutable'");
            migrationBuilder.Sql("CREATE TRIGGER trg_notification_events_no_delete BEFORE DELETE ON notification_events FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Notification events cannot be deleted'");
            migrationBuilder.Sql("CREATE TRIGGER trg_notification_deliveries_no_delete BEFORE DELETE ON notification_deliveries FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Notification deliveries cannot be deleted'");
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_notification_deliveries_guard_update BEFORE UPDATE ON notification_deliveries
                FOR EACH ROW BEGIN
                  IF NOT (OLD.delivery_id <=> NEW.delivery_id) OR NOT (OLD.center_id <=> NEW.center_id)
                    OR NOT (OLD.event_id <=> NEW.event_id) OR NOT (OLD.user_id <=> NEW.user_id)
                    OR NOT (OLD.delivered_at <=> NEW.delivered_at)
                    OR (OLD.read_at IS NOT NULL AND NOT (OLD.read_at <=> NEW.read_at)) THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Only the first read receipt may be recorded';
                  END IF;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_events_no_update");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_events_no_delete");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_deliveries_guard_update");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_deliveries_no_delete");
            // Permissions may already be in active system roles after startup reconciliation.
            migrationBuilder.Sql("DELETE rp FROM role_permissions rp JOIN permissions p ON p.permission_id=rp.permission_id WHERE p.permission_code IN ('notifications.inbox.read_own','notifications.inbox.update_own','platform.notifications.read_own','platform.notifications.update_own')");
            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropTable(
                name: "notification_dispatches");

            migrationBuilder.DropTable(
                name: "notification_events");

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "454c6647-6007-52be-8ab4-76fb15d58a9a" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Student", "454c6647-6007-52be-8ab4-76fb15d58a9a" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "454c6647-6007-52be-8ab4-76fb15d58a9a" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "8d5472e5-47d3-5d9a-afca-e744135687e7" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "b0a8d388-56fe-5dbe-8f84-f6dedb768bfc" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "b6ebd012-bfe1-5e03-884c-43aafd32f1b8" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Student", "b6ebd012-bfe1-5e03-884c-43aafd32f1b8" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "b6ebd012-bfe1-5e03-884c-43aafd32f1b8" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "454c6647-6007-52be-8ab4-76fb15d58a9a");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "8d5472e5-47d3-5d9a-afca-e744135687e7");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "b0a8d388-56fe-5dbe-8f84-f6dedb768bfc");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "b6ebd012-bfe1-5e03-884c-43aafd32f1b8");
        }
    }
}
