using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_reply",
                table: "notification_messages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "notification_messages",
                type: "varchar(32)",
                nullable: false,
                defaultValue: "Information");

            migrationBuilder.AddColumn<string>(
                name: "correction_of_message_id",
                table: "notification_messages",
                type: "varchar(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority",
                table: "notification_messages",
                type: "varchar(16)",
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<bool>(
                name: "requires_acknowledgement",
                table: "notification_messages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "acknowledged_at",
                table: "notification_deliveries",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "hidden_at",
                table: "notification_deliveries",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_notification_deliveries_center_id_delivery_id",
                table: "notification_deliveries",
                columns: new[] { "center_id", "delivery_id" });

            migrationBuilder.CreateTable(
                name: "notification_message_actions",
                columns: table => new
                {
                    action_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    message_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    client_action_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    replacement_message_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    kind = table.Column<string>(type: "varchar(16)", nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_message_actions", x => x.action_id);
                    table.CheckConstraint("ck_notification_message_action_kind", "`kind` IN ('Retracted','Corrected') AND ((`kind` = 'Corrected' AND `replacement_message_id` IS NOT NULL) OR (`kind` = 'Retracted' AND `replacement_message_id` IS NULL))");
                    table.ForeignKey(
                        name: "FK_notification_message_actions_notification_messages_center_id~",
                        columns: x => new { x.center_id, x.message_id },
                        principalTable: "notification_messages",
                        principalColumns: new[] { "center_id", "message_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_message_actions_notification_messages_center_i~1",
                        columns: x => new { x.center_id, x.replacement_message_id },
                        principalTable: "notification_messages",
                        principalColumns: new[] { "center_id", "message_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_message_actions_users_center_id_created_by",
                        columns: x => new { x.center_id, x.created_by },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notification_replies",
                columns: table => new
                {
                    reply_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    delivery_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    client_reply_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    body = table.Column<string>(type: "varchar(2000)", nullable: false),
                    sender_name = table.Column<string>(type: "varchar(160)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_replies", x => x.reply_id);
                    table.ForeignKey(
                        name: "FK_notification_replies_notification_deliveries_center_id_deliv~",
                        columns: x => new { x.center_id, x.delivery_id },
                        principalTable: "notification_deliveries",
                        principalColumns: new[] { "center_id", "delivery_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_replies_users_center_id_created_by",
                        columns: x => new { x.center_id, x.created_by },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "1c4fe07a-5b6a-5eff-9e1a-36a904b8d266", "retract_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép retract_own messages trong phạm vi được cấp.", true, true, "Notifications", "notifications.messages.retract_own", "Messages", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "2104cc1b-b652-5b58-88c6-c5c6cf00f2a7", "retract_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép retract_own notifications trong phạm vi được cấp.", false, true, "Platform", "platform.notifications.retract_own", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "77513671-4037-5b5b-8c3a-0f5037ba96c3", "reply_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép reply_own notifications trong phạm vi được cấp.", false, false, "Platform", "platform.notifications.reply_own", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "8f895c9e-fbc1-5f80-90fe-fe2420dbcd59", "reply_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép reply_own inbox trong phạm vi được cấp.", true, false, "Notifications", "notifications.inbox.reply_own", "Inbox", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "bedc7d8e-13eb-5989-95ec-f89820aee944", "acknowledge_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép acknowledge_own notifications trong phạm vi được cấp.", false, false, "Platform", "platform.notifications.acknowledge_own", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "fa4ad781-fe1d-54df-a313-dca5c236371a", "acknowledge_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép acknowledge_own inbox trong phạm vi được cấp.", true, false, "Notifications", "notifications.inbox.acknowledge_own", "Inbox", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "CenterManager", "1c4fe07a-5b6a-5eff-9e1a-36a904b8d266", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "1c4fe07a-5b6a-5eff-9e1a-36a904b8d266", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "2104cc1b-b652-5b58-88c6-c5c6cf00f2a7", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "77513671-4037-5b5b-8c3a-0f5037ba96c3", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "8f895c9e-fbc1-5f80-90fe-fe2420dbcd59", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "8f895c9e-fbc1-5f80-90fe-fe2420dbcd59", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "8f895c9e-fbc1-5f80-90fe-fe2420dbcd59", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "bedc7d8e-13eb-5989-95ec-f89820aee944", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "fa4ad781-fe1d-54df-a313-dca5c236371a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Student", "fa4ad781-fe1d-54df-a313-dca5c236371a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "fa4ad781-fe1d-54df-a313-dca5c236371a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_messages_center_id_correction_of_message_id",
                table: "notification_messages",
                columns: new[] { "center_id", "correction_of_message_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_message_actions_center_id_created_by_client_act~",
                table: "notification_message_actions",
                columns: new[] { "center_id", "created_by", "client_action_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_message_actions_center_id_message_id",
                table: "notification_message_actions",
                columns: new[] { "center_id", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_message_actions_center_id_replacement_message_id",
                table: "notification_message_actions",
                columns: new[] { "center_id", "replacement_message_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_replies_center_id_created_by",
                table: "notification_replies",
                columns: new[] { "center_id", "created_by" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_replies_center_id_delivery_id",
                table: "notification_replies",
                columns: new[] { "center_id", "delivery_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_notification_messages_notification_messages_center_id_correc~",
                table: "notification_messages",
                columns: new[] { "center_id", "correction_of_message_id" },
                principalTable: "notification_messages",
                principalColumns: new[] { "center_id", "message_id" },
                onDelete: ReferentialAction.Restrict);
            GuardReceipts(migrationBuilder, true);
            foreach (var table in new[] { "notification_message_actions", "notification_replies" })
            {
                migrationBuilder.Sql($"CREATE TRIGGER trg_{table}_no_update BEFORE UPDATE ON {table} FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Notification history is immutable'");
                migrationBuilder.Sql($"CREATE TRIGGER trg_{table}_no_delete BEFORE DELETE ON {table} FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Notification history cannot be deleted'");
            }
        }

        /// <inheritdoc />
        private static void GuardReceipts(MigrationBuilder migrationBuilder, bool lifecycle)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_deliveries_guard_update");
            var extra = lifecycle ? "OR (OLD.acknowledged_at IS NOT NULL AND NOT (OLD.acknowledged_at <=> NEW.acknowledged_at)) OR (OLD.hidden_at IS NOT NULL AND NOT (OLD.hidden_at <=> NEW.hidden_at))" : "";
            migrationBuilder.Sql($"""
                CREATE TRIGGER trg_notification_deliveries_guard_update BEFORE UPDATE ON notification_deliveries
                FOR EACH ROW BEGIN
                  IF NOT (OLD.delivery_id <=> NEW.delivery_id) OR NOT (OLD.center_id <=> NEW.center_id)
                    OR NOT (OLD.event_id <=> NEW.event_id) OR NOT (OLD.user_id <=> NEW.user_id)
                    OR NOT (OLD.delivered_at <=> NEW.delivered_at)
                    OR (OLD.read_at IS NOT NULL AND NOT (OLD.read_at <=> NEW.read_at)) {extra} THEN
                    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Only first notification receipts may be recorded';
                  END IF;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "notification_message_actions", "notification_replies" })
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{table}_no_update");
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS trg_{table}_no_delete");
            }
            GuardReceipts(migrationBuilder, false);
            migrationBuilder.Sql("DELETE rp FROM role_permissions rp JOIN permissions p ON p.permission_id=rp.permission_id WHERE p.permission_code IN ('notifications.messages.retract_own','platform.notifications.retract_own','notifications.inbox.acknowledge_own','notifications.inbox.reply_own','platform.notifications.acknowledge_own','platform.notifications.reply_own')");
            migrationBuilder.DropForeignKey(
                name: "FK_notification_messages_notification_messages_center_id_correc~",
                table: "notification_messages");

            migrationBuilder.DropTable(
                name: "notification_message_actions");

            migrationBuilder.DropTable(
                name: "notification_replies");

            migrationBuilder.DropIndex(
                name: "IX_notification_messages_center_id_correction_of_message_id",
                table: "notification_messages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_notification_deliveries_center_id_delivery_id",
                table: "notification_deliveries");

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "1c4fe07a-5b6a-5eff-9e1a-36a904b8d266" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "1c4fe07a-5b6a-5eff-9e1a-36a904b8d266" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "2104cc1b-b652-5b58-88c6-c5c6cf00f2a7" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "77513671-4037-5b5b-8c3a-0f5037ba96c3" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "8f895c9e-fbc1-5f80-90fe-fe2420dbcd59" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Student", "8f895c9e-fbc1-5f80-90fe-fe2420dbcd59" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "8f895c9e-fbc1-5f80-90fe-fe2420dbcd59" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "bedc7d8e-13eb-5989-95ec-f89820aee944" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "fa4ad781-fe1d-54df-a313-dca5c236371a" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Student", "fa4ad781-fe1d-54df-a313-dca5c236371a" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "fa4ad781-fe1d-54df-a313-dca5c236371a" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "1c4fe07a-5b6a-5eff-9e1a-36a904b8d266");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "2104cc1b-b652-5b58-88c6-c5c6cf00f2a7");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "77513671-4037-5b5b-8c3a-0f5037ba96c3");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "8f895c9e-fbc1-5f80-90fe-fe2420dbcd59");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "bedc7d8e-13eb-5989-95ec-f89820aee944");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "fa4ad781-fe1d-54df-a313-dca5c236371a");

            migrationBuilder.DropColumn(
                name: "allow_reply",
                table: "notification_messages");

            migrationBuilder.DropColumn(
                name: "category",
                table: "notification_messages");

            migrationBuilder.DropColumn(
                name: "correction_of_message_id",
                table: "notification_messages");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "notification_messages");

            migrationBuilder.DropColumn(
                name: "requires_acknowledgement",
                table: "notification_messages");

            migrationBuilder.DropColumn(
                name: "acknowledged_at",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "hidden_at",
                table: "notification_deliveries");
        }
    }
}
