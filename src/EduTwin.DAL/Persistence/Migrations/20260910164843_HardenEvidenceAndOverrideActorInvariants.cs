using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenEvidenceAndOverrideActorInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_reasoning_analyses_teachers_overridden_by_teacher",
                table: "reasoning_analyses");

            migrationBuilder.RenameColumn(
                name: "overridden_by_teacher_id",
                table: "reasoning_analyses",
                newName: "overridden_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_reasoning_analyses_center_id_overridden_by_teacher_id",
                table: "reasoning_analyses",
                newName: "ix_reasoning_analyses_center_id_overridden_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_reasoning_analyses_users_overridden_by_user",
                table: "reasoning_analyses",
                columns: new[] { "center_id", "overridden_by_user_id" },
                principalTable: "users",
                principalColumns: new[] { "center_id", "user_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER tr_evidence_no_self_supersede
                AFTER INSERT ON evidence_assessments
                FOR EACH ROW
                BEGIN
                    IF NEW.supersedes_assessment_id IS NOT NULL
                       AND NEW.supersedes_assessment_id = NEW.evidence_assessment_id THEN
                        SIGNAL SQLSTATE '45000'
                            SET MESSAGE_TEXT = 'Evidence assessment cannot supersede itself.';
                    END IF;
                END
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER tr_evidence_append_only_update
                BEFORE UPDATE ON evidence_assessments
                FOR EACH ROW
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'Evidence assessments are append-only and cannot be updated.'
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER tr_evidence_append_only_delete
                BEFORE DELETE ON evidence_assessments
                FOR EACH ROW
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'Evidence assessments are append-only and cannot be deleted.'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_evidence_append_only_delete");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_evidence_append_only_update");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_evidence_no_self_supersede");

            migrationBuilder.DropForeignKey(
                name: "fk_reasoning_analyses_users_overridden_by_user",
                table: "reasoning_analyses");

            migrationBuilder.RenameColumn(
                name: "overridden_by_user_id",
                table: "reasoning_analyses",
                newName: "overridden_by_teacher_id");

            migrationBuilder.RenameIndex(
                name: "ix_reasoning_analyses_center_id_overridden_by_user_id",
                table: "reasoning_analyses",
                newName: "ix_reasoning_analyses_center_id_overridden_by_teacher_id");

            migrationBuilder.AddForeignKey(
                name: "fk_reasoning_analyses_teachers_overridden_by_teacher",
                table: "reasoning_analyses",
                columns: new[] { "center_id", "overridden_by_teacher_id" },
                principalTable: "teachers",
                principalColumns: new[] { "center_id", "teacher_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
