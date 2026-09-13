using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetCenterIdToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "target_center_id",
                table: "authorization_audit_logs",
                type: "varchar(36)",
                nullable: true);

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[] { "4e09f21c-6af9-5e3c-96d6-61f99343de4a", "read", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép read audit trong phạm vi được cấp.", false, true, "Platform", "platform.audit.read", "Audit", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[] { "PlatformAdmin", "4e09f21c-6af9-5e3c-96d6-61f99343de4a", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ix_auth_audit_center_target_center_created",
                table: "authorization_audit_logs",
                columns: new[] { "center_id", "target_center_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_authorization_audit_logs_target_center_id",
                table: "authorization_audit_logs",
                column: "target_center_id");

            migrationBuilder.AddForeignKey(
                name: "fk_authorization_audit_logs_centers_target",
                table: "authorization_audit_logs",
                column: "target_center_id",
                principalTable: "centers",
                principalColumn: "center_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_authorization_audit_logs_centers_target",
                table: "authorization_audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_auth_audit_center_target_center_created",
                table: "authorization_audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_authorization_audit_logs_target_center_id",
                table: "authorization_audit_logs");

            migrationBuilder.Sql("DELETE FROM `role_permissions` WHERE `permission_id` = '4e09f21c-6af9-5e3c-96d6-61f99343de4a';");

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "PlatformAdmin", "4e09f21c-6af9-5e3c-96d6-61f99343de4a" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "4e09f21c-6af9-5e3c-96d6-61f99343de4a");

            migrationBuilder.DropColumn(
                name: "target_center_id",
                table: "authorization_audit_logs");
        }
    }
}
