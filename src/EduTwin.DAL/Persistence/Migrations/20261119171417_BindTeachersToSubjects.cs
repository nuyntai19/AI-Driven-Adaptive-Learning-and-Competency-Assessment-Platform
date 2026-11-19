using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindTeachersToSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "subject_id",
                table: "teachers",
                type: "VARCHAR(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_teachers_center_id_subject_id",
                table: "teachers",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_teachers_subjects",
                table: "teachers",
                columns: new[] { "center_id", "subject_id" },
                principalTable: "subjects",
                principalColumns: new[] { "center_id", "subject_id" },
                onDelete: ReferentialAction.Restrict);

            // Existing class assignments are authoritative only when they identify one subject.
            migrationBuilder.Sql("""
                UPDATE teachers t
                JOIN (
                    SELECT c.center_id, c.teacher_id, MIN(c.subject_id) AS subject_id
                    FROM classes c
                    JOIN subjects s ON s.center_id = c.center_id AND s.subject_id = c.subject_id AND s.is_deleted = 0
                    WHERE c.is_deleted = 0
                    GROUP BY c.center_id, c.teacher_id
                    HAVING COUNT(DISTINCT c.subject_id) = 1
                ) resolved ON resolved.center_id = t.center_id AND resolved.teacher_id = t.teacher_id
                SET t.subject_id = resolved.subject_id, t.row_version = t.row_version + 1
                WHERE t.subject_id IS NULL;
                """);

            // Resolve exact legacy department names/codes; leave ambiguous profiles for the manager.
            migrationBuilder.Sql("""
                UPDATE teachers t
                JOIN (
                    SELECT legacy.center_id, legacy.teacher_id, MIN(s.subject_id) AS subject_id
                    FROM teachers legacy
                    JOIN subjects s ON s.center_id = legacy.center_id AND s.is_deleted = 0
                        AND (TRIM(legacy.department) = s.subject_name OR TRIM(legacy.department) = s.subject_code)
                    WHERE legacy.subject_id IS NULL
                        AND NOT EXISTS (
                            SELECT 1 FROM classes c
                            WHERE c.center_id = legacy.center_id AND c.teacher_id = legacy.teacher_id
                                AND c.is_deleted = 0 AND c.subject_id <> s.subject_id
                        )
                    GROUP BY legacy.center_id, legacy.teacher_id
                    HAVING COUNT(DISTINCT s.subject_id) = 1
                ) resolved ON resolved.center_id = t.center_id AND resolved.teacher_id = t.teacher_id
                SET t.subject_id = resolved.subject_id, t.row_version = t.row_version + 1
                WHERE t.subject_id IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_teachers_subjects",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "IX_teachers_center_id_subject_id",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "subject_id",
                table: "teachers");
        }
    }
}
