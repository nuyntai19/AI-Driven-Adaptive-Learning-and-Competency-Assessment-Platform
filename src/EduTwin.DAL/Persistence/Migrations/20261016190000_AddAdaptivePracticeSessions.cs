using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptivePracticeSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adaptive_practice_sessions",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    student_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    subject_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    topic_node_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false),
                    learning_path_id = table.Column<string>(type: "VARCHAR(36)", nullable: true),
                    learning_path_item_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: true),
                    practice_mode = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    starting_mastery = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: false),
                    target_mastery = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: false),
                    current_mastery = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: false),
                    target_skill_ids = table.Column<string>(type: "TEXT", nullable: true),
                    time_budget_minutes = table.Column<int>(type: "INT", nullable: false),
                    minimum_questions = table.Column<int>(type: "INT", nullable: false),
                    maximum_questions = table.Column<int>(type: "INT", nullable: false),
                    question_count = table.Column<int>(type: "INT", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    close_reason = table.Column<string>(type: "VARCHAR(64)", nullable: true),
                    selection_reason = table.Column<string>(type: "TEXT", nullable: false),
                    policy_version = table.Column<string>(type: "VARCHAR(16)", nullable: false, defaultValue: "1.0"),
                    current_difficulty = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false, defaultValue: (byte)2),
                    consecutive_correct = table.Column<int>(type: "INT", nullable: false, defaultValue: 0),
                    consecutive_incorrect = table.Column<int>(type: "INT", nullable: false, defaultValue: 0),
                    is_diagnostic_mode = table.Column<bool>(type: "TINYINT(1)", nullable: false, defaultValue: false),
                    diagnostic_questions_count = table.Column<int>(type: "INT", nullable: false, defaultValue: 0),
                    prerequisite_tested_node_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: true),
                    started_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    paused_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
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
                    table.PrimaryKey("pk_adaptive_practice_sessions", x => x.session_id);
                    table.UniqueConstraint("ux_adaptive_practice_sessions_center_id_session_id", x => new { x.center_id, x.session_id });
                    table.ForeignKey(
                        name: "fk_adaptive_practice_sessions_students",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_adaptive_practice_sessions_subjects",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_adaptive_practice_sessions_knowledge_nodes",
                        columns: x => new { x.center_id, x.topic_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_adaptive_practice_sessions_center_student_status",
                table: "adaptive_practice_sessions",
                columns: new[] { "center_id", "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_adaptive_practice_sessions_center_student_subject",
                table: "adaptive_practice_sessions",
                columns: new[] { "center_id", "student_id", "subject_id" });

            migrationBuilder.CreateTable(
                name: "adaptive_practice_session_items",
                columns: table => new
                {
                    session_item_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    session_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    question_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false),
                    knowledge_node_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: false),
                    difficulty = table.Column<byte>(type: "TINYINT UNSIGNED", nullable: false),
                    sequence_number = table.Column<int>(type: "INT", nullable: false),
                    selection_reason_code = table.Column<string>(type: "VARCHAR(64)", nullable: false),
                    selection_reason_text = table.Column<string>(type: "TEXT", nullable: false),
                    question_version = table.Column<string>(type: "VARCHAR(16)", nullable: false, defaultValue: "1.0"),
                    presented_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    answered_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: true),
                    status = table.Column<string>(type: "VARCHAR(32)", nullable: false),
                    last_attempt_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: true),
                    hints_used_count = table.Column<int>(type: "INT", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "DATETIME(6)", nullable: false),
                    created_by = table.Column<string>(type: "VARCHAR(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adaptive_practice_session_items", x => x.session_item_id);
                    table.UniqueConstraint("ux_adaptive_practice_session_items_center_id_session_item_id", x => new { x.center_id, x.session_item_id });
                    table.ForeignKey(
                        name: "fk_adaptive_practice_session_items_sessions",
                        columns: x => new { x.center_id, x.session_id },
                        principalTable: "adaptive_practice_sessions",
                        principalColumns: new[] { "center_id", "session_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_adaptive_practice_session_items_questions",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_adaptive_practice_session_items_nodes",
                        columns: x => new { x.center_id, x.knowledge_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_adaptive_practice_session_items_center_session_seq",
                table: "adaptive_practice_session_items",
                columns: new[] { "center_id", "session_id", "sequence_number" });

            migrationBuilder.CreateTable(
                name: "adaptive_practice_session_summaries",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    center_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    student_id = table.Column<string>(type: "VARCHAR(36)", nullable: false),
                    total_questions = table.Column<int>(type: "INT", nullable: false),
                    correct_count = table.Column<int>(type: "INT", nullable: false),
                    incorrect_count = table.Column<int>(type: "INT", nullable: false),
                    partial_count = table.Column<int>(type: "INT", nullable: false),
                    pending_count = table.Column<int>(type: "INT", nullable: false),
                    time_spent_seconds = table.Column<int>(type: "INT", nullable: false),
                    starting_mastery = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: false),
                    ending_mastery = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: true),
                    goal_achievement_status = table.Column<string>(type: "VARCHAR(64)", nullable: false),
                    strong_skill_ids = table.Column<string>(type: "TEXT", nullable: true),
                    weak_skill_ids = table.Column<string>(type: "TEXT", nullable: true),
                    remediation_skill_ids = table.Column<string>(type: "TEXT", nullable: true),
                    summary_conclusion = table.Column<string>(type: "TEXT", nullable: false),
                    recommended_next_action = table.Column<string>(type: "TEXT", nullable: false),
                    recommended_next_topic_id = table.Column<ulong>(type: "BIGINT UNSIGNED", nullable: true),
                    summary_status = table.Column<string>(type: "VARCHAR(32)", nullable: false, defaultValue: "Finalized"),
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
                    table.PrimaryKey("pk_adaptive_practice_session_summaries", x => x.session_id);
                    table.UniqueConstraint("ux_adaptive_practice_summaries_center_id_session_id", x => new { x.center_id, x.session_id });
                    table.ForeignKey(
                        name: "fk_adaptive_practice_summaries_sessions",
                        columns: x => new { x.center_id, x.session_id },
                        principalTable: "adaptive_practice_sessions",
                        principalColumns: new[] { "center_id", "session_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_adaptive_practice_summaries_students",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_adaptive_practice_summaries_topics",
                        columns: x => new { x.center_id, x.recommended_next_topic_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_adaptive_practice_summaries_center_student",
                table: "adaptive_practice_session_summaries",
                columns: new[] { "center_id", "student_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "adaptive_practice_session_summaries");
            migrationBuilder.DropTable(name: "adaptive_practice_session_items");
            migrationBuilder.DropTable(name: "adaptive_practice_sessions");
        }
    }
}
