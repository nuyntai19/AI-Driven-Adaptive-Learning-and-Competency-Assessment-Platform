using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLearningPathStrategyCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_learning_paths_strategy",
                table: "learning_paths");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[] { "98f42873-3a16-5011-8e01-7fa178914093", "update_own", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép update_own student trong phạm vi được cấp.", true, false, "Recommendations", "recommendations.student.update_own", "Student", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[] { "Student", "98f42873-3a16-5011-8e01-7fa178914093", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.AddCheckConstraint(
                name: "ck_learning_paths_strategy",
                table: "learning_paths",
                sql: "`strategy` IN ('LinearFallback', 'OpportunityGap', 'MaintenanceReview')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_learning_paths_strategy",
                table: "learning_paths");

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "Student", "98f42873-3a16-5011-8e01-7fa178914093" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "98f42873-3a16-5011-8e01-7fa178914093");

            migrationBuilder.AddCheckConstraint(
                name: "ck_learning_paths_strategy",
                table: "learning_paths",
                sql: "`strategy` IN ('LinearFallback', 'OpportunityGap')");
        }
    }
}
