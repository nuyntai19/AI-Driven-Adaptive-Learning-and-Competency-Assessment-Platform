using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EduTwinDbContext))]
    [Migration("20260920153000_AddAiSolutionToReasoningAnalysis")]
    public partial class AddAiSolutionToReasoningAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "solution_type",
                table: "reasoning_analyses",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_solution",
                table: "reasoning_analyses",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reasoning_analyses_solution_type",
                table: "reasoning_analyses",
                sql: "`solution_type` IS NULL OR `solution_type` IN ('REFINED', 'CORRECTED', 'GENERATED', 'MODEL_ANSWER')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reasoning_analyses_solution_type",
                table: "reasoning_analyses");

            migrationBuilder.DropColumn(
                name: "ai_solution",
                table: "reasoning_analyses");

            migrationBuilder.DropColumn(
                name: "solution_type",
                table: "reasoning_analyses");
        }
    }
}
