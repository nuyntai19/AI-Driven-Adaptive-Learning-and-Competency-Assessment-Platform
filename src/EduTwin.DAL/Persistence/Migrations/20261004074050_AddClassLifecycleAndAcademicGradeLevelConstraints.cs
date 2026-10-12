using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassLifecycleAndAcademicGradeLevelConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "grade_level",
                table: "questions",
                type: "tinyint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "grade_level",
                table: "curriculums",
                type: "tinyint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "grade_level",
                table: "classes",
                type: "TINYINT UNSIGNED",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "grade_level_at_enrollment",
                table: "class_students",
                type: "TINYINT UNSIGNED",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "grade_mismatch_reason",
                table: "class_students",
                type: "VARCHAR(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exception_approved_by",
                table: "class_students",
                type: "VARCHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "exception_approved_at",
                table: "class_students",
                type: "DATETIME(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_grade_mismatch",
                table: "assignments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "grade_mismatch_reason",
                table: "assignments",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_mode",
                table: "assignments",
                type: "varchar(32)",
                nullable: false,
                defaultValue: "WholeClass");

            migrationBuilder.Sql(@"
                UPDATE assignments a
                SET a.target_mode = 'GapGroup'
                WHERE EXISTS (
                    SELECT 1 FROM assignment_targets at
                    WHERE at.assignment_id = a.assignment_id
                      AND at.target_source IN ('GapGroup', '2')
                );

                UPDATE assignments a
                SET a.target_mode = 'SelectedStudents'
                WHERE EXISTS (
                    SELECT 1 FROM assignment_targets at
                    WHERE at.assignment_id = a.assignment_id
                      AND at.target_source IN ('SelectedStudents', '1')
                ) AND a.target_mode != 'GapGroup';
            ");


            migrationBuilder.AddCheckConstraint(
                name: "ck_assignments_target_mode",
                table: "assignments",
                sql: "target_mode IN ('WholeClass', 'SelectedStudents', 'GapGroup')");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "permission_id", "action_name", "created_at", "description", "is_delegable", "is_sensitive", "module_name", "permission_code", "resource_name", "status", "updated_at" },
                values: new object[] { "3d5c0d09-1c18-50d1-85d1-0f8190335264", "delete", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc), "Cho phép delete classes trong phạm vi được cấp.", true, true, "Organization", "organization.classes.delete", "Classes", "Active", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "permission_account_types",
                columns: new[] { "account_type", "permission_id", "created_at" },
                values: new object[] { "CenterManager", "3d5c0d09-1c18-50d1-85d1-0f8190335264", new DateTime(2026, 9, 9, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ix_questions_center_id_subject_id_grade_level",
                table: "questions",
                columns: new[] { "center_id", "subject_id", "grade_level" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_grade_level",
                table: "questions",
                sql: "grade_level IS NULL OR (grade_level >= 10 AND grade_level <= 12)");

            migrationBuilder.CreateIndex(
                name: "ix_curriculums_center_id_subject_id_grade_level",
                table: "curriculums",
                columns: new[] { "center_id", "subject_id", "grade_level" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_curriculums_grade_level",
                table: "curriculums",
                sql: "grade_level IS NULL OR (grade_level >= 10 AND grade_level <= 12)");

            migrationBuilder.CreateIndex(
                name: "ix_classes_center_id_grade_level",
                table: "classes",
                columns: new[] { "center_id", "grade_level" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_classes_grade_level",
                table: "classes",
                sql: "grade_level IS NULL OR (grade_level >= 10 AND grade_level <= 12)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_questions_center_id_subject_id_grade_level",
                table: "questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_grade_level",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "ix_curriculums_center_id_subject_id_grade_level",
                table: "curriculums");

            migrationBuilder.DropCheckConstraint(
                name: "ck_curriculums_grade_level",
                table: "curriculums");

            migrationBuilder.DropIndex(
                name: "ix_classes_center_id_grade_level",
                table: "classes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_classes_grade_level",
                table: "classes");

            migrationBuilder.DeleteData(
                table: "permission_account_types",
                keyColumns: new[] { "account_type", "permission_id" },
                keyValues: new object[] { "CenterManager", "3d5c0d09-1c18-50d1-85d1-0f8190335264" });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "permission_id",
                keyValue: "3d5c0d09-1c18-50d1-85d1-0f8190335264");

            migrationBuilder.DropColumn(
                name: "grade_level",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "grade_level",
                table: "curriculums");

            migrationBuilder.DropColumn(
                name: "grade_level",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "exception_approved_at",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "exception_approved_by",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "grade_mismatch_reason",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "grade_level_at_enrollment",
                table: "class_students");

            migrationBuilder.DropColumn(
                name: "allow_grade_mismatch",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "grade_mismatch_reason",
                table: "assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignments_target_mode",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "target_mode",
                table: "assignments");
        }
    }
}
