using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredCoordinateGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions");

            migrationBuilder.AddColumn<string>(
                name: "preliminary_grading_reason_code",
                table: "attempts",
                type: "varchar(64)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions",
                sql: "answer_evaluation_mode IN ('TextExact', 'NumericRational', 'Manual', 'Coordinate2D')");

            // Targeted backfill for the known structured-coordinate seed. The predicates
            // intentionally avoid reclassifying arbitrary free-text short answers.
            migrationBuilder.Sql(
                """
                UPDATE questions
                SET answer_evaluation_mode = 'Coordinate2D'
                WHERE question_type = 'ShortAnswer'
                  AND answer_evaluation_mode = 'TextExact'
                  AND question_text = 'Tìm tọa độ đỉnh của parabol y = -2x^2 + 4x - 1.'
                  AND correct_answer = '(1, 1)';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE questions
                SET answer_evaluation_mode = 'TextExact'
                WHERE question_type = 'ShortAnswer'
                  AND answer_evaluation_mode = 'Coordinate2D'
                  AND question_text = 'Tìm tọa độ đỉnh của parabol y = -2x^2 + 4x - 1.'
                  AND correct_answer = '(1, 1)';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "preliminary_grading_reason_code",
                table: "attempts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_answer_evaluation_mode",
                table: "questions",
                sql: "answer_evaluation_mode IN ('TextExact', 'NumericRational', 'Manual')");
        }
    }
}
