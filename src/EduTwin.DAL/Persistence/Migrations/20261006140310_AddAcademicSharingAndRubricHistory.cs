using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicSharingAndRubricHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rubric_result",
                table: "teacher_review_histories",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "questions",
                type: "varchar(16)",
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "curriculums",
                type: "varchar(16)",
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.CreateIndex(
                name: "ix_questions_center_visibility_status",
                table: "questions",
                columns: new[] { "center_id", "visibility", "status" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_visibility",
                table: "questions",
                sql: "visibility IN ('Private', 'Shared')");

            migrationBuilder.CreateIndex(
                name: "ix_curriculums_center_visibility_status",
                table: "curriculums",
                columns: new[] { "center_id", "visibility", "review_status" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_curriculums_visibility",
                table: "curriculums",
                sql: "visibility IN ('Private', 'Shared')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_questions_center_visibility_status",
                table: "questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_visibility",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "ix_curriculums_center_visibility_status",
                table: "curriculums");

            migrationBuilder.DropCheckConstraint(
                name: "ck_curriculums_visibility",
                table: "curriculums");

            migrationBuilder.DropColumn(
                name: "rubric_result",
                table: "teacher_review_histories");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "curriculums");
        }
    }
}
