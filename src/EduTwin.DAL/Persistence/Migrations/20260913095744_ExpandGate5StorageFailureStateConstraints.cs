using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandGate5StorageFailureStateConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attempts_status",
                table: "attempts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_analysis_jobs_retry_count",
                table: "ai_analysis_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attempts_status",
                table: "attempts",
                sql: "`status` IN ('PendingAnalysis', 'Processing', 'Completed', 'NeedsTeacherReview', 'AnalysisFailed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_analysis_jobs_retry_count",
                table: "ai_analysis_jobs",
                sql: "`retry_count` BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attempts_status",
                table: "attempts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_analysis_jobs_retry_count",
                table: "ai_analysis_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attempts_status",
                table: "attempts",
                sql: "`status` IN ('PendingAnalysis', 'Processing', 'Completed', 'NeedsTeacherReview')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_analysis_jobs_retry_count",
                table: "ai_analysis_jobs",
                sql: "`retry_count` BETWEEN 0 AND 1");
        }
    }
}
