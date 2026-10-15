using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningPathSessionsAndRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "session_run_id",
                table: "attempts",
                type: "bigint unsigned",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "learning_path_sessions",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    learning_path_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    legacy_session_id = table.Column<string>(type: "varchar(128)", nullable: true),
                    topic_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    class_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    phase_number = table.Column<uint>(type: "int unsigned", nullable: false),
                    week_number = table.Column<uint>(type: "int unsigned", nullable: false),
                    session_order = table.Column<uint>(type: "int unsigned", nullable: false),
                    title = table.Column<string>(type: "varchar(255)", nullable: false),
                    objective = table.Column<string>(type: "longtext", nullable: false),
                    rationale = table.Column<string>(type: "longtext", nullable: true),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    practice_question_ids = table.Column<string>(type: "json", nullable: true),
                    checkpoint_question_ids = table.Column<string>(type: "json", nullable: true),
                    minimum_questions = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 1u),
                    required_correct_rate = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0.70m),
                    policy_version = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "session-policy-v1"),
                    current_run_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    total_runs = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learning_path_sessions", x => x.session_id);
                    table.UniqueConstraint("ux_learning_path_sessions_center_id_session_id", x => new { x.center_id, x.session_id });
                    table.CheckConstraint("ck_learning_path_sessions_required_correct_rate", "`required_correct_rate` BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_learning_path_sessions_status", "`status` IN ('NotStarted', 'InProgress', 'Completed', 'NeedsReview', 'Skipped')");
                    table.ForeignKey(
                        name: "fk_learning_path_sessions_classes_class",
                        columns: x => new { x.center_id, x.class_id },
                        principalTable: "classes",
                        principalColumns: new[] { "center_id", "class_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_path_sessions_knowledge_nodes_topic_node",
                        columns: x => new { x.center_id, x.topic_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_path_sessions_learning_paths_learning_path",
                        columns: x => new { x.center_id, x.learning_path_id },
                        principalTable: "learning_paths",
                        principalColumns: new[] { "center_id", "learning_path_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "learning_path_session_runs",
                columns: table => new
                {
                    session_run_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    session_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    run_number = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 1u),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    evaluation_status = table.Column<string>(type: "varchar(32)", nullable: false),
                    outcome = table.Column<string>(type: "varchar(32)", nullable: false),
                    active_key = table.Column<sbyte>(type: "tinyint", nullable: true, computedColumnSql: "CASE WHEN `status` IN ('InProgress', 'Grading') THEN 1 ELSE NULL END", stored: true),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    current_question_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    checkpoint_question_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    questions_assigned = table.Column<string>(type: "json", nullable: true),
                    policy_snapshot = table.Column<string>(type: "json", nullable: true),
                    total_questions = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    questions_attempted = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    questions_correct = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    score_rate = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    starting_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ending_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learning_path_session_runs", x => x.session_run_id);
                    table.UniqueConstraint("ux_learning_path_session_runs_center_id_session_run_id", x => new { x.center_id, x.session_run_id });
                    table.CheckConstraint("ck_learning_path_session_runs_evaluation_status", "`evaluation_status` IN ('NotStarted', 'Queued', 'Processing', 'AwaitingTeacher', 'Completed', 'Failed')");
                    table.CheckConstraint("ck_learning_path_session_runs_outcome", "`outcome` IN ('None', 'Passed', 'NotPassed', 'InsufficientEvidence')");
                    table.CheckConstraint("ck_learning_path_session_runs_status", "`status` IN ('InProgress', 'Grading', 'NeedsReview', 'Completed', 'Abandoned')");
                    table.ForeignKey(
                        name: "fk_learning_path_session_runs_questions_checkpoint_question",
                        columns: x => new { x.center_id, x.checkpoint_question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_path_session_runs_questions_current_question",
                        columns: x => new { x.center_id, x.current_question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_path_session_runs_sessions_session",
                        columns: x => new { x.center_id, x.session_id },
                        principalTable: "learning_path_sessions",
                        principalColumns: new[] { "center_id", "session_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_path_session_runs_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_attempts_center_id_session_run_id",
                table: "attempts",
                columns: new[] { "center_id", "session_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_path_session_runs_center_id_checkpoint_question_id",
                table: "learning_path_session_runs",
                columns: new[] { "center_id", "checkpoint_question_id" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_path_session_runs_center_id_current_question_id",
                table: "learning_path_session_runs",
                columns: new[] { "center_id", "current_question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_session_runs_center_session_run_number",
                table: "learning_path_session_runs",
                columns: new[] { "center_id", "session_id", "run_number" });

            migrationBuilder.CreateIndex(
                name: "ix_session_runs_center_student_status",
                table: "learning_path_session_runs",
                columns: new[] { "center_id", "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_session_runs_center_student_session_active_key",
                table: "learning_path_session_runs",
                columns: new[] { "center_id", "student_id", "session_id", "active_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_learning_path_sessions_center_id_class_id",
                table: "learning_path_sessions",
                columns: new[] { "center_id", "class_id" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_path_sessions_center_id_topic_node_id",
                table: "learning_path_sessions",
                columns: new[] { "center_id", "topic_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lp_sessions_center_path_session_order",
                table: "learning_path_sessions",
                columns: new[] { "center_id", "learning_path_id", "session_order" });

            migrationBuilder.CreateIndex(
                name: "ux_learning_path_sessions_center_id_legacy_session_id",
                table: "learning_path_sessions",
                columns: new[] { "center_id", "legacy_session_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_attempts_session_runs_session_run",
                table: "attempts",
                columns: new[] { "center_id", "session_run_id" },
                principalTable: "learning_path_session_runs",
                principalColumns: new[] { "center_id", "session_run_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attempts_session_runs_session_run",
                table: "attempts");

            migrationBuilder.DropTable(
                name: "learning_path_session_runs");

            migrationBuilder.DropTable(
                name: "learning_path_sessions");

            migrationBuilder.DropIndex(
                name: "ix_attempts_center_id_session_run_id",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "session_run_id",
                table: "attempts");
        }
    }
}
