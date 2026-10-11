using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationManualSend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "message_id",
                table: "notification_events",
                type: "varchar(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sender_name",
                table: "notification_events",
                type: "varchar(160)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notification_messages",
                columns: table => new
                {
                    message_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    sender_user_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    client_message_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    sender_name = table.Column<string>(type: "varchar(160)", nullable: false),
                    sender_auth_version = table.Column<uint>(type: "int unsigned", nullable: false),
                    title = table.Column<string>(type: "varchar(160)", nullable: false),
                    body = table.Column<string>(type: "varchar(2000)", nullable: false),
                    audience = table.Column<string>(type: "json", nullable: false),
                    fingerprint = table.Column<string>(type: "varchar(64)", nullable: false),
                    recipient_count = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_messages", x => x.message_id);
                    table.UniqueConstraint("AK_notification_messages_center_id_message_id", x => new { x.center_id, x.message_id });
                    table.ForeignKey(
                        name: "FK_notification_messages_centers_center_id",
                        column: x => x.center_id,
                        principalTable: "centers",
                        principalColumn: "center_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_messages_users_center_id_sender_user_id",
                        columns: x => new { x.center_id, x.sender_user_id },
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
                    { "0212f221-7e16-5a6f-9328-22513eed6167", "read_sent_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_sent_own notifications trong phạm vi được cấp.", false, false, "Platform", "platform.notifications.read_sent_own", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "528655c4-a256-5766-b9cb-8bc7d5f9d6fd", "send_scoped", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép send_scoped notifications trong phạm vi được cấp.", false, true, "Platform", "platform.notifications.send_scoped", "Notifications", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "b59b8d4c-4091-5754-bb01-0f9d1997a78e", "read_sent_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read_sent_own messages trong phạm vi được cấp.", true, false, "Notifications", "notifications.messages.read_sent_own", "Messages", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "d559ad30-e923-5bfd-a61c-cf9611ac1ace", "send_scoped", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép send_scoped messages trong phạm vi được cấp.", true, true, "Notifications", "notifications.messages.send_scoped", "Messages", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "PlatformAdmin", "0212f221-7e16-5a6f-9328-22513eed6167", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "PlatformAdmin", "528655c4-a256-5766-b9cb-8bc7d5f9d6fd", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "b59b8d4c-4091-5754-bb01-0f9d1997a78e", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "b59b8d4c-4091-5754-bb01-0f9d1997a78e", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "d559ad30-e923-5bfd-a61c-cf9611ac1ace", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "Teacher", "d559ad30-e923-5bfd-a61c-cf9611ac1ace", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_events_message_id",
                table: "notification_events",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_messages_center_id_sender_user_id_client_messag~",
                table: "notification_messages",
                columns: new[] { "center_id", "sender_user_id", "client_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_messages_center_id_sender_user_id_created_at",
                table: "notification_messages",
                columns: new[] { "center_id", "sender_user_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_notification_events_notification_messages_message_id",
                table: "notification_events",
                column: "message_id",
                principalTable: "notification_messages",
                principalColumn: "message_id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("CREATE TRIGGER trg_notification_messages_no_update BEFORE UPDATE ON notification_messages FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Sent notification messages are immutable'");
            migrationBuilder.Sql("CREATE TRIGGER trg_notification_messages_no_delete BEFORE DELETE ON notification_messages FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Sent notification messages cannot be deleted'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_messages_no_update");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_notification_messages_no_delete");
            migrationBuilder.Sql("DELETE rp FROM role_permissions rp JOIN permissions p ON p.permission_id=rp.permission_id WHERE p.permission_code IN ('notifications.messages.send_scoped','notifications.messages.read_sent_own','platform.notifications.send_scoped','platform.notifications.read_sent_own')");
            migrationBuilder.DropForeignKey(
                name: "FK_notification_events_notification_messages_message_id",
                table: "notification_events");

            migrationBuilder.DropTable(
                name: "notification_messages");

            migrationBuilder.DropIndex(
                name: "IX_notification_events_message_id",
                table: "notification_events");

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "0212f221-7e16-5a6f-9328-22513eed6167" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "528655c4-a256-5766-b9cb-8bc7d5f9d6fd" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "b59b8d4c-4091-5754-bb01-0f9d1997a78e" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "b59b8d4c-4091-5754-bb01-0f9d1997a78e" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "d559ad30-e923-5bfd-a61c-cf9611ac1ace" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Teacher", "d559ad30-e923-5bfd-a61c-cf9611ac1ace" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "0212f221-7e16-5a6f-9328-22513eed6167");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "528655c4-a256-5766-b9cb-8bc7d5f9d6fd");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "b59b8d4c-4091-5754-bb01-0f9d1997a78e");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "d559ad30-e923-5bfd-a61c-cf9611ac1ace");

            migrationBuilder.DropColumn(
                name: "message_id",
                table: "notification_events");

            migrationBuilder.DropColumn(
                name: "sender_name",
                table: "notification_events");
        }
    }
}
