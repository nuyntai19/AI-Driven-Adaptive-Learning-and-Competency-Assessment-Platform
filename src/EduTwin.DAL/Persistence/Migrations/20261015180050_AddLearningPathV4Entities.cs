using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningPathV4Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_content_versions",
                columns: table => new
                {
                    content_version_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    skill_id = table.Column<string>(type: "varchar(64)", nullable: false),
                    activity_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    title = table.Column<string>(type: "varchar(255)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    estimated_duration_minutes = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 15u),
                    payload = table.Column<string>(type: "json", nullable: false),
                    rubric = table.Column<string>(type: "json", nullable: true),
                    prerequisites = table.Column<string>(type: "json", nullable: true),
                    version = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 1u),
                    status = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Published"),
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
                    table.PrimaryKey("pk_learning_content_versions", x => x.content_version_id);
                    table.UniqueConstraint("ux_learning_content_versions_center_id_content_version_id", x => new { x.center_id, x.content_version_id });
                    table.ForeignKey(
                        name: "fk_learning_content_versions_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "learning_goals",
                columns: table => new
                {
                    goal_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    class_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    goal_type = table.Column<string>(type: "varchar(64)", nullable: false),
                    title = table.Column<string>(type: "varchar(255)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    target_skill_ids = table.Column<string>(type: "json", nullable: true),
                    deadline_date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    available_minutes_per_day = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 15u),
                    available_days_per_week = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 5u),
                    preferred_pace = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Moderate"),
                    preferred_mode = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Balanced"),
                    status = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Active"),
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
                    table.PrimaryKey("pk_learning_goals", x => x.goal_id);
                    table.UniqueConstraint("ux_learning_goals_center_id_goal_id", x => new { x.center_id, x.goal_id });
                    table.ForeignKey(
                        name: "fk_learning_goals_classes_class",
                        columns: x => new { x.center_id, x.class_id },
                        principalTable: "classes",
                        principalColumns: new[] { "center_id", "class_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_goals_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_goals_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "path_decisions",
                columns: table => new
                {
                    decision_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    learning_path_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    session_run_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    trigger_type = table.Column<string>(type: "varchar(64)", nullable: false),
                    reason_codes = table.Column<string>(type: "json", nullable: false),
                    evidence_reference_ids = table.Column<string>(type: "json", nullable: true),
                    decision_summary = table.Column<string>(type: "text", nullable: false),
                    policy_version = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "v4-pilot-1"),
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
                    table.PrimaryKey("pk_path_decisions", x => x.decision_id);
                    table.UniqueConstraint("ux_path_decisions_center_id_decision_id", x => new { x.center_id, x.decision_id });
                    table.ForeignKey(
                        name: "fk_path_decisions_learning_paths_learning_path",
                        columns: x => new { x.center_id, x.learning_path_id },
                        principalTable: "learning_paths",
                        principalColumns: new[] { "center_id", "learning_path_id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "activity_runs",
                columns: table => new
                {
                    activity_run_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    session_run_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    content_version_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    activity_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    activity_order = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 1u),
                    status = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "NotStarted"),
                    draft_payload = table.Column<string>(type: "longtext", nullable: true),
                    draft_revision = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 1u),
                    assistance_requested_count = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    assistance_mode = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Normal"),
                    score = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    max_score = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    is_mastered = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("pk_activity_runs", x => x.activity_run_id);
                    table.UniqueConstraint("ux_activity_runs_center_id_activity_run_id", x => new { x.center_id, x.activity_run_id });
                    table.ForeignKey(
                        name: "fk_activity_runs_content_versions_content_version",
                        columns: x => new { x.center_id, x.content_version_id },
                        principalTable: "learning_content_versions",
                        principalColumns: new[] { "center_id", "content_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_activity_runs_session_runs_session_run",
                        columns: x => new { x.center_id, x.session_run_id },
                        principalTable: "learning_path_session_runs",
                        principalColumns: new[] { "center_id", "session_run_id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "path_proposals",
                columns: table => new
                {
                    proposal_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    goal_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    starting_baseline_summary = table.Column<string>(type: "text", nullable: false),
                    candidate_plan = table.Column<string>(type: "json", nullable: false),
                    feasibility_status = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Feasible"),
                    feasibility_message = table.Column<string>(type: "text", nullable: true),
                    estimated_weeks = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 4u),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Pending"),
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
                    table.PrimaryKey("pk_path_proposals", x => x.proposal_id);
                    table.UniqueConstraint("ux_path_proposals_center_id_proposal_id", x => new { x.center_id, x.proposal_id });
                    table.ForeignKey(
                        name: "fk_path_proposals_learning_goals_goal",
                        columns: x => new { x.center_id, x.goal_id },
                        principalTable: "learning_goals",
                        principalColumns: new[] { "center_id", "goal_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_path_proposals_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_path_proposals_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "issued_items",
                columns: table => new
                {
                    issued_item_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    activity_run_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    item_order = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 1u),
                    exposure_mode = table.Column<string>(type: "varchar(32)", nullable: false, defaultValue: "Interactive"),
                    issued_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    solution_exposed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("pk_issued_items", x => x.issued_item_id);
                    table.UniqueConstraint("ux_issued_items_center_id_issued_item_id", x => new { x.center_id, x.issued_item_id });
                    table.ForeignKey(
                        name: "fk_issued_items_activity_runs_activity_run",
                        columns: x => new { x.center_id, x.activity_run_id },
                        principalTable: "activity_runs",
                        principalColumns: new[] { "center_id", "activity_run_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_issued_items_attempts_attempt",
                        columns: x => new { x.center_id, x.attempt_id },
                        principalTable: "attempts",
                        principalColumns: new[] { "center_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_activity_runs_center_id_content_version_id",
                table: "activity_runs",
                columns: new[] { "center_id", "content_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_activity_runs_center_session_run_order",
                table: "activity_runs",
                columns: new[] { "center_id", "session_run_id", "activity_order" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_items_center_activity_run_order",
                table: "issued_items",
                columns: new[] { "center_id", "activity_run_id", "item_order" });

            migrationBuilder.CreateIndex(
                name: "IX_issued_items_center_id_attempt_id",
                table: "issued_items",
                columns: new[] { "center_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_content_versions_center_subject_skill_status",
                table: "learning_content_versions",
                columns: new[] { "center_id", "subject_id", "skill_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_goals_center_id_class_id",
                table: "learning_goals",
                columns: new[] { "center_id", "class_id" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_goals_center_id_subject_id",
                table: "learning_goals",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_goals_center_student_subject_status",
                table: "learning_goals",
                columns: new[] { "center_id", "student_id", "subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_path_decisions_center_learning_path_created_at",
                table: "path_decisions",
                columns: new[] { "center_id", "learning_path_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_path_proposals_center_id_goal_id",
                table: "path_proposals",
                columns: new[] { "center_id", "goal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_path_proposals_center_id_subject_id",
                table: "path_proposals",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_path_proposals_center_student_status",
                table: "path_proposals",
                columns: new[] { "center_id", "student_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issued_items");

            migrationBuilder.DropTable(
                name: "path_decisions");

            migrationBuilder.DropTable(
                name: "path_proposals");

            migrationBuilder.DropTable(
                name: "activity_runs");

            migrationBuilder.DropTable(
                name: "learning_goals");

            migrationBuilder.DropTable(
                name: "learning_content_versions");
        }
    }
}
