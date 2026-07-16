using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentAIJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attempts",
                columns: table => new
                {
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    assignment_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    final_answer = table.Column<string>(type: "longtext", nullable: false),
                    reasoning_text = table.Column<string>(type: "longtext", nullable: true),
                    is_correct = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    awarded_score = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    time_spent_seconds = table.Column<uint>(type: "int unsigned", nullable: false),
                    confidence = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    answer_changes = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    skipped = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    reasoning_language = table.Column<string>(type: "varchar(8)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    client_submission_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempts", x => x.attempt_id);
                    table.UniqueConstraint("ux_attempts_center_id_attempt_id", x => new { x.center_id, x.attempt_id });
                    table.CheckConstraint("ck_attempts_confidence", "`confidence` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_attempts_reasoning_language", "`reasoning_language` IN ('vi', 'en')");
                    table.CheckConstraint("ck_attempts_status", "`status` IN ('PendingAnalysis', 'Processing', 'Completed', 'NeedsTeacherReview')");
                    table.CheckConstraint("ck_attempts_time_spent_seconds", "`time_spent_seconds` >= 0");
                    table.ForeignKey(
                        name: "fk_attempts_assignments_assignment",
                        columns: x => new { x.center_id, x.assignment_id },
                        principalTable: "assignments",
                        principalColumns: new[] { "center_id", "assignment_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attempts_questions_question",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attempts_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ai_analysis_jobs",
                columns: table => new
                {
                    analysis_job_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    retry_count = table.Column<byte>(type: "tinyint unsigned", nullable: false, defaultValue: (byte)0),
                    available_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    lease_owner = table.Column<string>(type: "varchar(100)", nullable: true),
                    lease_until = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_error_code = table.Column<string>(type: "varchar(100)", nullable: true),
                    last_error_message = table.Column<string>(type: "varchar(1000)", nullable: true),
                    correlation_id = table.Column<string>(type: "varchar(64)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_analysis_jobs", x => x.analysis_job_id);
                    table.CheckConstraint("ck_ai_analysis_jobs_retry_count", "`retry_count` BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_ai_analysis_jobs_status", "`status` IN ('Pending', 'Processing', 'Completed', 'FallbackCompleted', 'FailedTerminal')");
                    table.ForeignKey(
                        name: "fk_ai_analysis_jobs_attempts_attempt",
                        columns: x => new { x.center_id, x.attempt_id },
                        principalTable: "attempts",
                        principalColumns: new[] { "center_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "reasoning_analyses",
                columns: table => new
                {
                    analysis_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    schema_version = table.Column<string>(type: "varchar(20)", nullable: false),
                    method_detected = table.Column<string>(type: "varchar(500)", nullable: true),
                    reasoning_quality = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    error_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    misconception = table.Column<string>(type: "varchar(1000)", nullable: true),
                    missing_steps = table.Column<string>(type: "json", nullable: false),
                    root_cause_node_ids = table.Column<string>(type: "json", nullable: false),
                    analysis_confidence = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    feedback = table.Column<string>(type: "longtext", nullable: false),
                    is_fallback = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    needs_teacher_review = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    provider = table.Column<string>(type: "varchar(32)", nullable: false),
                    model_name = table.Column<string>(type: "varchar(100)", nullable: true),
                    override_reasoning_quality = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    override_error_type = table.Column<string>(type: "varchar(32)", nullable: true),
                    override_feedback = table.Column<string>(type: "longtext", nullable: true),
                    override_is_correct = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    override_reason = table.Column<string>(type: "varchar(1000)", nullable: true),
                    overridden_by_teacher_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    overridden_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    override_version = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reasoning_analyses", x => x.analysis_id);
                    table.UniqueConstraint("ux_reasoning_analyses_center_id_analysis_id", x => new { x.center_id, x.analysis_id });
                    table.CheckConstraint("ck_reasoning_analyses_analysis_confidence", "`analysis_confidence` IS NULL OR `analysis_confidence` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_reasoning_analyses_error_type", "`error_type` IN ('None', 'Knowledge', 'Skill', 'Reasoning', 'Behavior', 'Presentation', 'Unknown')");
                    table.CheckConstraint("ck_reasoning_analyses_override_error_type", "`override_error_type` IS NULL OR `override_error_type` IN ('None', 'Knowledge', 'Skill', 'Reasoning', 'Behavior', 'Presentation', 'Unknown')");
                    table.CheckConstraint("ck_reasoning_analyses_override_reasoning_quality", "`override_reasoning_quality` IS NULL OR `override_reasoning_quality` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_reasoning_analyses_provider", "`provider` IN ('Gemini', 'RuleBased')");
                    table.CheckConstraint("ck_reasoning_analyses_reasoning_quality", "`reasoning_quality` IS NULL OR `reasoning_quality` BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_reasoning_analyses_attempts_attempt",
                        columns: x => new { x.center_id, x.attempt_id },
                        principalTable: "attempts",
                        principalColumns: new[] { "center_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reasoning_analyses_teachers_overridden_by_teacher",
                        columns: x => new { x.center_id, x.overridden_by_teacher_id },
                        principalTable: "teachers",
                        principalColumns: new[] { "center_id", "teacher_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_twin_update_history_center_id_analysis_id",
                table: "twin_update_history",
                columns: new[] { "center_id", "analysis_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_center_id_source_attempt_id",
                table: "recommendations",
                columns: new[] { "center_id", "source_attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_paths_center_id_generated_from_attempt_id",
                table: "learning_paths",
                columns: new[] { "center_id", "generated_from_attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_twins_center_id_last_attempt_id",
                table: "knowledge_twins",
                columns: new[] { "center_id", "last_attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_analysis_jobs_center_id_status_created_at",
                table: "ai_analysis_jobs",
                columns: new[] { "center_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_analysis_jobs_status_available_at_lease_until",
                table: "ai_analysis_jobs",
                columns: new[] { "status", "available_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "ux_ai_analysis_jobs_center_id_attempt_id",
                table: "ai_analysis_jobs",
                columns: new[] { "center_id", "attempt_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attempts_center_id_assignment_id_student_id",
                table: "attempts",
                columns: new[] { "center_id", "assignment_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attempts_center_id_question_id",
                table: "attempts",
                columns: new[] { "center_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attempts_center_id_status_created_at",
                table: "attempts",
                columns: new[] { "center_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attempts_center_id_student_id_question_id_created_at",
                table: "attempts",
                columns: new[] { "center_id", "student_id", "question_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_attempts_center_id_student_id_client_submission_id",
                table: "attempts",
                columns: new[] { "center_id", "student_id", "client_submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reasoning_analyses_center_id_needs_teacher_review_created_at",
                table: "reasoning_analyses",
                columns: new[] { "center_id", "needs_teacher_review", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reasoning_analyses_center_id_overridden_by_teacher_id",
                table: "reasoning_analyses",
                columns: new[] { "center_id", "overridden_by_teacher_id" });

            migrationBuilder.CreateIndex(
                name: "ux_reasoning_analyses_center_id_attempt_id",
                table: "reasoning_analyses",
                columns: new[] { "center_id", "attempt_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_twins_attempts_last_attempt",
                table: "knowledge_twins",
                columns: new[] { "center_id", "last_attempt_id" },
                principalTable: "attempts",
                principalColumns: new[] { "center_id", "attempt_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_learning_paths_attempts_generated_from_attempt",
                table: "learning_paths",
                columns: new[] { "center_id", "generated_from_attempt_id" },
                principalTable: "attempts",
                principalColumns: new[] { "center_id", "attempt_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_recommendations_attempts_source_attempt",
                table: "recommendations",
                columns: new[] { "center_id", "source_attempt_id" },
                principalTable: "attempts",
                principalColumns: new[] { "center_id", "attempt_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_twin_update_history_attempts_attempt",
                table: "twin_update_history",
                columns: new[] { "center_id", "attempt_id" },
                principalTable: "attempts",
                principalColumns: new[] { "center_id", "attempt_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_twin_update_history_reasoning_analyses_analysis",
                table: "twin_update_history",
                columns: new[] { "center_id", "analysis_id" },
                principalTable: "reasoning_analyses",
                principalColumns: new[] { "center_id", "analysis_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_twins_attempts_last_attempt",
                table: "knowledge_twins");

            migrationBuilder.DropForeignKey(
                name: "fk_learning_paths_attempts_generated_from_attempt",
                table: "learning_paths");

            migrationBuilder.DropForeignKey(
                name: "fk_recommendations_attempts_source_attempt",
                table: "recommendations");

            migrationBuilder.DropForeignKey(
                name: "fk_twin_update_history_attempts_attempt",
                table: "twin_update_history");

            migrationBuilder.DropForeignKey(
                name: "fk_twin_update_history_reasoning_analyses_analysis",
                table: "twin_update_history");

            migrationBuilder.DropTable(
                name: "ai_analysis_jobs");

            migrationBuilder.DropTable(
                name: "reasoning_analyses");

            migrationBuilder.DropTable(
                name: "attempts");

            migrationBuilder.DropIndex(
                name: "ix_twin_update_history_center_id_analysis_id",
                table: "twin_update_history");

            migrationBuilder.DropIndex(
                name: "ix_recommendations_center_id_source_attempt_id",
                table: "recommendations");

            migrationBuilder.DropIndex(
                name: "ix_learning_paths_center_id_generated_from_attempt_id",
                table: "learning_paths");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_twins_center_id_last_attempt_id",
                table: "knowledge_twins");
        }
    }
}
