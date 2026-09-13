using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillQuestionEvaluationModeMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE questions SET answer_evaluation_mode = 'Manual' WHERE question_type = 'Essay';");
            migrationBuilder.Sql("UPDATE questions SET answer_evaluation_mode = 'TextExact' WHERE question_type = 'MultipleChoice';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill is not strictly reversible without losing original modes, but safe to leave as-is on rollback.
        }
    }
}
