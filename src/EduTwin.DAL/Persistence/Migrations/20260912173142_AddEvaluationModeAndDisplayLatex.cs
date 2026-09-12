using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationModeAndDisplayLatex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "answer_evaluation_mode",
                table: "questions",
                type: "varchar(32)",
                nullable: false,
                defaultValue: "TextExact");

            migrationBuilder.AddColumn<string>(
                name: "answer_display_latex",
                table: "attempts",
                type: "varchar(2048)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions",
                sql: "answer_evaluation_mode IN ('TextExact', 'NumericRational', 'Manual')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "answer_evaluation_mode",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "answer_display_latex",
                table: "attempts");
        }
    }
}
