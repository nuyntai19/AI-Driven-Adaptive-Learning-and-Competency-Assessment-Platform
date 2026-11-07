using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherStudentNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teacher_student_notes",
                columns: table => new
                {
                    note_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    teacher_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    student_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    updated_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_student_notes", x => x.note_id);
                    table.UniqueConstraint("ux_teacher_student_notes_center_id_note_id", x => new { x.center_id, x.note_id });
                    table.ForeignKey(
                        name: "fk_teacher_student_notes_teachers",
                        columns: x => new { x.center_id, x.teacher_id },
                        principalTable: "teachers",
                        principalColumns: new[] { "center_id", "teacher_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_student_notes_students",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_student_notes_center_id_student_id",
                table: "teacher_student_notes",
                columns: new[] { "center_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_teacher_student_notes_center_id_teacher_id_student_id",
                table: "teacher_student_notes",
                columns: new[] { "center_id", "teacher_id", "student_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teacher_student_notes");
        }
    }
}
