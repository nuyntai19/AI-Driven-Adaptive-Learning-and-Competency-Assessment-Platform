using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreMissingModulePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adaptive_practice_sessions",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    topic_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    learning_path_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    learning_path_item_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    practice_mode = table.Column<string>(type: "varchar(32)", nullable: false),
                    starting_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    target_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    current_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    target_skill_ids = table.Column<string>(type: "text", nullable: true),
                    time_budget_minutes = table.Column<int>(type: "int", nullable: false),
                    minimum_questions = table.Column<int>(type: "int", nullable: false),
                    maximum_questions = table.Column<int>(type: "int", nullable: false),
                    question_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    close_reason = table.Column<string>(type: "varchar(64)", nullable: true),
                    selection_reason = table.Column<string>(type: "text", nullable: false),
                    policy_version = table.Column<string>(type: "varchar(16)", nullable: false, defaultValue: "1.0"),
                    current_difficulty = table.Column<byte>(type: "tinyint unsigned", nullable: false, defaultValue: (byte)2),
                    consecutive_correct = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    consecutive_incorrect = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    is_diagnostic_mode = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    diagnostic_questions_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    prerequisite_tested_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    paused_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adaptive_practice_sessions", x => x.session_id);
                    table.UniqueConstraint("ux_adaptive_practice_sessions_center_id_session_id", x => new { x.center_id, x.session_id });
                    table.ForeignKey(
                        name: "FK_adaptive_practice_sessions_knowledge_nodes_center_id_topic_n~",
                        columns: x => new { x.center_id, x.topic_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adaptive_practice_sessions_learning_paths_learning_path_id",
                        column: x => x.learning_path_id,
                        principalTable: "learning_paths",
                        principalColumn: "learning_path_id");
                    table.ForeignKey(
                        name: "FK_adaptive_practice_sessions_students_center_id_student_id",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adaptive_practice_sessions_subjects_center_id_subject_id",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "adaptive_practice_session_items",
                columns: table => new
                {
                    session_item_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    session_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    knowledge_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    difficulty = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    sequence_number = table.Column<int>(type: "int", nullable: false),
                    selection_reason_code = table.Column<string>(type: "varchar(64)", nullable: false),
                    selection_reason_text = table.Column<string>(type: "text", nullable: false),
                    question_version = table.Column<string>(type: "varchar(16)", nullable: false, defaultValue: "1.0"),
                    presented_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    answered_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    last_attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    hints_used_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adaptive_practice_session_items", x => x.session_item_id);
                    table.UniqueConstraint("ux_adaptive_practice_session_items_center_id_session_item_id", x => new { x.center_id, x.session_item_id });
                    table.ForeignKey(
                        name: "FK_adaptive_practice_session_items_adaptive_practice_sessions_c~",
                        columns: x => new { x.center_id, x.session_id },
                        principalTable: "adaptive_practice_sessions",
                        principalColumns: new[] { "center_id", "session_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_adaptive_practice_session_items_knowledge_nodes_center_id_kn~",
                        columns: x => new { x.center_id, x.knowledge_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adaptive_practice_session_items_questions_center_id_question~",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "adaptive_practice_session_summaries",
                columns: table => new
                {
                    session_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    total_questions = table.Column<int>(type: "int", nullable: false),
                    correct_count = table.Column<int>(type: "int", nullable: false),
                    incorrect_count = table.Column<int>(type: "int", nullable: false),
                    partial_count = table.Column<int>(type: "int", nullable: false),
                    pending_count = table.Column<int>(type: "int", nullable: false),
                    time_spent_seconds = table.Column<int>(type: "int", nullable: false),
                    starting_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ending_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    goal_achievement_status = table.Column<string>(type: "varchar(64)", nullable: false),
                    strong_skill_ids = table.Column<string>(type: "text", nullable: true),
                    weak_skill_ids = table.Column<string>(type: "text", nullable: true),
                    remediation_skill_ids = table.Column<string>(type: "text", nullable: true),
                    summary_conclusion = table.Column<string>(type: "text", nullable: false),
                    recommended_next_action = table.Column<string>(type: "text", nullable: false),
                    recommended_next_topic_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    summary_status = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Finalized"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adaptive_practice_session_summaries", x => x.session_id);
                    table.UniqueConstraint("ux_adaptive_practice_summaries_center_id_session_id", x => new { x.center_id, x.session_id });
                    table.ForeignKey(
                        name: "FK_adaptive_practice_session_summaries_adaptive_practice_sessio~",
                        columns: x => new { x.center_id, x.session_id },
                        principalTable: "adaptive_practice_sessions",
                        principalColumns: new[] { "center_id", "session_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_adaptive_practice_session_summaries_knowledge_nodes_center_i~",
                        columns: x => new { x.center_id, x.recommended_next_topic_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_adaptive_practice_session_summaries_students_center_id_stude~",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_adaptive_practice_session_items_center_id_knowledge_node_id",
                table: "adaptive_practice_session_items",
                columns: new[] { "center_id", "knowledge_node_id" });

            migrationBuilder.CreateIndex(
                name: "IX_adaptive_practice_session_items_center_id_question_id",
                table: "adaptive_practice_session_items",
                columns: new[] { "center_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_adaptive_practice_session_items_center_session_seq",
                table: "adaptive_practice_session_items",
                columns: new[] { "center_id", "session_id", "sequence_number" });

            migrationBuilder.CreateIndex(
                name: "IX_adaptive_practice_session_summaries_center_id_recommended_ne~",
                table: "adaptive_practice_session_summaries",
                columns: new[] { "center_id", "recommended_next_topic_id" });

            migrationBuilder.CreateIndex(
                name: "ix_adaptive_practice_summaries_center_student",
                table: "adaptive_practice_session_summaries",
                columns: new[] { "center_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "IX_adaptive_practice_sessions_center_id_subject_id",
                table: "adaptive_practice_sessions",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "IX_adaptive_practice_sessions_center_id_topic_node_id",
                table: "adaptive_practice_sessions",
                columns: new[] { "center_id", "topic_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_adaptive_practice_sessions_center_student_status",
                table: "adaptive_practice_sessions",
                columns: new[] { "center_id", "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_adaptive_practice_sessions_center_student_subject",
                table: "adaptive_practice_sessions",
                columns: new[] { "center_id", "student_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "IX_adaptive_practice_sessions_learning_path_id",
                table: "adaptive_practice_sessions",
                column: "learning_path_id");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adaptive_practice_session_items");

            migrationBuilder.DropTable(
                name: "adaptive_practice_session_summaries");

            migrationBuilder.DropTable(
                name: "adaptive_practice_sessions");
        }
    }
}
