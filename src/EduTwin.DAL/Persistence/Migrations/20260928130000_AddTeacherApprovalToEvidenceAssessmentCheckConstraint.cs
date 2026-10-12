using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherApprovalToEvidenceAssessmentCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_assessments_source_type",
                table: "evidence_assessments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_assessments_source_type",
                table: "evidence_assessments",
                sql: "`source_type` IN ('AI', 'RuleFallback', 'TeacherOverride', 'TeacherApproval')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evidence_assessments_source_type",
                table: "evidence_assessments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evidence_assessments_source_type",
                table: "evidence_assessments",
                sql: "`source_type` IN ('AI', 'RuleFallback', 'TeacherOverride')");
        }
    }
}
