using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentQuestionBookmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_question_bookmarks",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_question_bookmarks", x => new { x.center_id, x.student_id, x.question_id });
                    table.ForeignKey(
                        name: "FK_student_question_bookmarks_questions_center_id_question_id",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_student_question_bookmarks_question",
                table: "student_question_bookmarks",
                columns: new[] { "center_id", "question_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_question_bookmarks");
        }
    }
}
