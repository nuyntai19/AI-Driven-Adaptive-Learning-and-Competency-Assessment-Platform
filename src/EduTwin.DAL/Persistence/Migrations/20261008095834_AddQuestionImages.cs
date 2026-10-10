using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_image",
                table: "questions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "question_images",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    data = table.Column<byte[]>(type: "mediumblob", nullable: false),
                    sha256 = table.Column<string>(type: "varchar(64)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_images", x => new { x.center_id, x.question_id });
                    table.CheckConstraint("ck_question_images_size", "OCTET_LENGTH(`data`) BETWEEN 1 AND 2097152");
                    table.ForeignKey(
                        name: "fk_question_images_questions_question",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_images");

            migrationBuilder.DropColumn(
                name: "has_image",
                table: "questions");
        }
    }
}
