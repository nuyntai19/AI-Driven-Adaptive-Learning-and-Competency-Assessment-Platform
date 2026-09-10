using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOverrideAwardedScoreToReasoningAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "override_awarded_score",
                table: "reasoning_analyses",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reasoning_analyses_override_awarded_score",
                table: "reasoning_analyses",
                sql: "`override_awarded_score` IS NULL OR `override_awarded_score` >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reasoning_analyses_override_awarded_score",
                table: "reasoning_analyses");

            migrationBuilder.DropColumn(
                name: "override_awarded_score",
                table: "reasoning_analyses");
        }
    }
}
