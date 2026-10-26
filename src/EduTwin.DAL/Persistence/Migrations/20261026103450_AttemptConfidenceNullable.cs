using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttemptConfidenceNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "timezone",
                table: "centers",
                type: "VARCHAR(64)",
                nullable: false,
                defaultValue: "Asia/Ho_Chi_Minh",
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldDefaultValue: "Asia/Bangkok");

            migrationBuilder.AlterColumn<decimal>(
                name: "confidence",
                table: "attempts",
                type: "decimal(5,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "timezone",
                table: "centers",
                type: "VARCHAR(64)",
                nullable: false,
                defaultValue: "Asia/Bangkok",
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldDefaultValue: "Asia/Ho_Chi_Minh");

            migrationBuilder.AlterColumn<decimal>(
                name: "confidence",
                table: "attempts",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldNullable: true);
        }
    }
}
