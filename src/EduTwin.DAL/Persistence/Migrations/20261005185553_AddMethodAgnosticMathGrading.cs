using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMethodAgnosticMathGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions");

            migrationBuilder.AddColumn<string>(
                name: "answer_assessment",
                table: "reasoning_analyses",
                type: "varchar(16)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "feedback_origin",
                table: "reasoning_analyses",
                type: "varchar(24)",
                nullable: false,
                defaultValue: "LegacySystem");

            migrationBuilder.AddColumn<string>(
                name: "reasoning_verdict",
                table: "reasoning_analyses",
                type: "varchar(16)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions",
                sql: "answer_evaluation_mode IN ('TextExact', 'NumericRational', 'Manual', 'Coordinate2D', 'MathEquivalent')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions");

            // Rollback must defer new mathematical modes, never turn them into
            // literal matching and silently mark equivalent answers incorrect.
            migrationBuilder.Sql("UPDATE questions SET answer_evaluation_mode = 'Manual' WHERE answer_evaluation_mode = 'MathEquivalent';");

            migrationBuilder.DropColumn(
                name: "answer_assessment",
                table: "reasoning_analyses");

            migrationBuilder.DropColumn(
                name: "feedback_origin",
                table: "reasoning_analyses");

            migrationBuilder.DropColumn(
                name: "reasoning_verdict",
                table: "reasoning_analyses");

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions",
                sql: "answer_evaluation_mode IN ('TextExact', 'NumericRational', 'Manual', 'Coordinate2D')");
        }
    }
}
