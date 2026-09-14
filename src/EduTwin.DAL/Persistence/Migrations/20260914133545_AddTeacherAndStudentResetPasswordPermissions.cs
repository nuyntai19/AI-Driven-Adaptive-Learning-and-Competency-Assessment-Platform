using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherAndStudentResetPasswordPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[,]
                {
                    { "38aa5060-4615-5d50-8c81-cdf7a1fd6b39", "reset_password", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép reset_password teachers trong phạm vi được cấp.", true, true, "Organization", "organization.teachers.reset_password", "Teachers", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "5dc8cce2-04b2-5d8b-b632-7b1fe56c2c4b", "reset_password", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép reset_password students trong phạm vi được cấp.", true, true, "Organization", "organization.students.reset_password", "Students", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[,]
                {
                    { "CenterManager", "38aa5060-4615-5d50-8c81-cdf7a1fd6b39", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "CenterManager", "5dc8cce2-04b2-5d8b-b632-7b1fe56c2c4b", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "38aa5060-4615-5d50-8c81-cdf7a1fd6b39" });

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "5dc8cce2-04b2-5d8b-b632-7b1fe56c2c4b" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "38aa5060-4615-5d50-8c81-cdf7a1fd6b39");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "5dc8cce2-04b2-5d8b-b632-7b1fe56c2c4b");
        }
    }
}
