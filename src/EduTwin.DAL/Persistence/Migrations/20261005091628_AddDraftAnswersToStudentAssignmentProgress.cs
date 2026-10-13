using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftAnswersToStudentAssignmentProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "draft_answers_json",
                table: "student_assignment_progress",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "draft_saved_at",
                table: "student_assignment_progress",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "draft_answers_json",
                table: "student_assignment_progress");

            migrationBuilder.DropColumn(
                name: "draft_saved_at",
                table: "student_assignment_progress");
        }
    }
}
