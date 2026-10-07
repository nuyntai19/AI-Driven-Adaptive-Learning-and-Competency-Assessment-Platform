using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAIProcessingCoordination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_analysis_checkpoints",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    request_fingerprint = table.Column<string>(type: "varchar(64)", nullable: false),
                    response_json = table.Column<string>(type: "longtext", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_analysis_checkpoints", x => new { x.center_id, x.attempt_id });
                    table.ForeignKey(
                        name: "FK_ai_analysis_checkpoints_attempts_center_id_attempt_id",
                        columns: x => new { x.center_id, x.attempt_id },
                        principalTable: "attempts",
                        principalColumns: new[] { "center_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ai_provider_quota_states",
                columns: table => new
                {
                    pool_id = table.Column<string>(type: "varchar(64)", nullable: false),
                    state_json = table.Column<string>(type: "longtext", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_provider_quota_states", x => x.pool_id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ai_student_post_processing_jobs",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    assignment_scope_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    revision = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    processed_revision = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    source_attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    trigger_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    available_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    lease_owner = table.Column<string>(type: "varchar(100)", nullable: true),
                    lease_until = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    failure_count = table.Column<int>(type: "int", nullable: false),
                    last_error_code = table.Column<string>(type: "varchar(100)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_student_post_processing_jobs", x => new { x.center_id, x.student_id, x.subject_id, x.assignment_scope_id });
                    table.ForeignKey(
                        name: "FK_ai_student_post_processing_jobs_students_center_id_student_id",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ai_student_post_processing_jobs_center_id_available_at_lease~",
                table: "ai_student_post_processing_jobs",
                columns: new[] { "center_id", "available_at", "lease_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_analysis_checkpoints");

            migrationBuilder.DropTable(
                name: "ai_provider_quota_states");

            migrationBuilder.DropTable(
                name: "ai_student_post_processing_jobs");
        }
    }
}
