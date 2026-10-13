using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedVoidedFieldsToAssignmentQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_voided",
                table: "assignment_questions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "void_reason",
                table: "assignment_questions",
                type: "varchar(1000)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "voided_at",
                table: "assignment_questions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "voided_by_user_id",
                table: "assignment_questions",
                type: "varchar(36)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_voided",
                table: "assignment_questions");

            migrationBuilder.DropColumn(
                name: "void_reason",
                table: "assignment_questions");

            migrationBuilder.DropColumn(
                name: "voided_at",
                table: "assignment_questions");

            migrationBuilder.DropColumn(
                name: "voided_by_user_id",
                table: "assignment_questions");
        }
    }
}
