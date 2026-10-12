using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAIGradingProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "suggested_rubric_grade_json",
                table: "reasoning_analyses",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "suggested_score",
                table: "reasoning_analyses",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "uses_alternative_method",
                table: "reasoning_analyses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "suggested_rubric_grade_json",
                table: "reasoning_analyses");

            migrationBuilder.DropColumn(
                name: "suggested_score",
                table: "reasoning_analyses");

            migrationBuilder.DropColumn(
                name: "uses_alternative_method",
                table: "reasoning_analyses");
        }
    }
}
