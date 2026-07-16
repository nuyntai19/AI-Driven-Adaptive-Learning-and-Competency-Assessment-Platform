using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalTwinPersonalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "behavior_twins",
                columns: table => new
                {
                    behavior_twin_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    avg_time_spent_seconds = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    skip_rate = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    change_answer_rate = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    avg_confidence = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    confidence_calibration = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    attempt_count = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
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
                    table.PrimaryKey("pk_behavior_twins", x => x.behavior_twin_id);
                    table.CheckConstraint("ck_behavior_twins_avg_confidence", "`avg_confidence` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_behavior_twins_change_answer_rate", "`change_answer_rate` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_behavior_twins_confidence_calibration", "`confidence_calibration` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_behavior_twins_skip_rate", "`skip_rate` BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_behavior_twins_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_behavior_twins_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "knowledge_twins",
                columns: table => new
                {
                    knowledge_twin_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    topic_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    mastery_percentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    evidence_count = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    last_reasoning_quality = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    last_attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    last_evidence_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
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
                    table.PrimaryKey("pk_knowledge_twins", x => x.knowledge_twin_id);
                    table.CheckConstraint("ck_knowledge_twins_last_reasoning_quality", "`last_reasoning_quality` IS NULL OR `last_reasoning_quality` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_knowledge_twins_mastery_percentage", "`mastery_percentage` BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_knowledge_twins_knowledge_nodes_topic_node",
                        columns: x => new { x.center_id, x.topic_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_knowledge_twins_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_knowledge_twins_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "learning_paths",
                columns: table => new
                {
                    learning_path_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    strategy = table.Column<string>(type: "varchar(32)", nullable: false),
                    version = table.Column<uint>(type: "int unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    generated_from_attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    generated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
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
                    table.PrimaryKey("pk_learning_paths", x => x.learning_path_id);
                    table.UniqueConstraint("ux_learning_paths_center_id_learning_path_id", x => new { x.center_id, x.learning_path_id });
                    table.CheckConstraint("ck_learning_paths_status", "`status` IN ('Active', 'Superseded', 'Completed')");
                    table.CheckConstraint("ck_learning_paths_strategy", "`strategy` IN ('LinearFallback', 'OpportunityGap')");
                    table.ForeignKey(
                        name: "fk_learning_paths_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_paths_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    recommendation_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    topic_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    recommendation_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    opportunity_score = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    calculation_version = table.Column<string>(type: "varchar(20)", nullable: false),
                    calculation_breakdown = table.Column<string>(type: "json", nullable: false),
                    explanation = table.Column<string>(type: "varchar(1000)", nullable: false),
                    source_attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
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
                    table.PrimaryKey("pk_recommendations", x => x.recommendation_id);
                    table.CheckConstraint("ck_recommendations_opportunity_score", "`opportunity_score` IS NULL OR `opportunity_score` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_recommendations_recommendation_type", "`recommendation_type` IN ('TopicAndQuestion', 'LinearFallback')");
                    table.CheckConstraint("ck_recommendations_status", "`status` IN ('Active', 'Accepted', 'Dismissed', 'Superseded')");
                    table.ForeignKey(
                        name: "fk_recommendations_knowledge_nodes_topic_node",
                        columns: x => new { x.center_id, x.topic_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recommendations_questions_question",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recommendations_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recommendations_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "student_subject_goals",
                columns: table => new
                {
                    goal_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    target_score = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    remaining_days = table.Column<uint>(type: "int unsigned", nullable: false),
                    current_predicted_score = table.Column<decimal>(type: "decimal(4,2)", nullable: false, defaultValue: 0m),
                    risk_score = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
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
                    table.PrimaryKey("pk_student_subject_goals", x => x.goal_id);
                    table.CheckConstraint("ck_student_subject_goals_current_predicted_score", "`current_predicted_score` BETWEEN 0 AND 10");
                    table.CheckConstraint("ck_student_subject_goals_remaining_days", "remaining_days <= 3650");
                    table.CheckConstraint("ck_student_subject_goals_risk_score", "`risk_score` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_student_subject_goals_target_score", "`target_score` BETWEEN 0 AND 10");
                    table.ForeignKey(
                        name: "fk_student_subject_goals_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_subject_goals_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "student_twins",
                columns: table => new
                {
                    twin_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    overall_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    last_evidence_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
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
                    table.PrimaryKey("pk_student_twins", x => x.twin_id);
                    table.CheckConstraint("ck_student_twins_overall_mastery", "`overall_mastery` BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_student_twins_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "twin_update_history",
                columns: table => new
                {
                    history_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    topic_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    analysis_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    event_source = table.Column<string>(type: "varchar(32)", nullable: false),
                    previous_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    new_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    mastery_delta = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    effective_reasoning_quality = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    calculation_version = table.Column<string>(type: "varchar(20)", nullable: false),
                    calculation_breakdown = table.Column<string>(type: "json", nullable: false),
                    explanation = table.Column<string>(type: "varchar(1000)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_twin_update_history", x => x.history_id);
                    table.CheckConstraint("ck_twin_update_history_effective_reasoning_quality", "`effective_reasoning_quality` IS NULL OR `effective_reasoning_quality` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_twin_update_history_event_source", "`event_source` IN ('AIAnalysis', 'RuleFallback', 'TeacherOverride', 'Replay')");
                    table.CheckConstraint("ck_twin_update_history_new_mastery", "`new_mastery` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_twin_update_history_previous_mastery", "`previous_mastery` BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_twin_update_history_knowledge_nodes_topic_node",
                        columns: x => new { x.center_id, x.topic_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_twin_update_history_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_twin_update_history_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "learning_path_items",
                columns: table => new
                {
                    learning_path_item_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    learning_path_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    topic_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    recommended_question_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    rank_order = table.Column<uint>(type: "int unsigned", nullable: false),
                    opportunity_score = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    reason = table.Column<string>(type: "varchar(1000)", nullable: false),
                    status = table.Column<string>(type: "varchar(32)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
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
                    table.PrimaryKey("pk_learning_path_items", x => x.learning_path_item_id);
                    table.CheckConstraint("ck_learning_path_items_opportunity_score", "`opportunity_score` IS NULL OR `opportunity_score` BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_learning_path_items_status", "`status` IN ('Pending', 'Current', 'Completed', 'Skipped')");
                    table.ForeignKey(
                        name: "fk_learning_path_items_knowledge_nodes_topic_node",
                        columns: x => new { x.center_id, x.topic_node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_path_items_learning_paths_learning_path",
                        columns: x => new { x.center_id, x.learning_path_id },
                        principalTable: "learning_paths",
                        principalColumns: new[] { "center_id", "learning_path_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_learning_path_items_questions_recommended_question",
                        columns: x => new { x.center_id, x.recommended_question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_behavior_twins_center_id_subject_id",
                table: "behavior_twins",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ux_behavior_twins_center_id_student_id_subject_id",
                table: "behavior_twins",
                columns: new[] { "center_id", "student_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_twins_center_id_subject_id_mastery_percentage",
                table: "knowledge_twins",
                columns: new[] { "center_id", "subject_id", "mastery_percentage" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_twins_center_id_topic_node_id",
                table: "knowledge_twins",
                columns: new[] { "center_id", "topic_node_id" });

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_twins_center_id_student_id_topic_node_id",
                table: "knowledge_twins",
                columns: new[] { "center_id", "student_id", "topic_node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_learning_path_items_center_id_recommended_question_id",
                table: "learning_path_items",
                columns: new[] { "center_id", "recommended_question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_path_items_center_id_topic_node_id",
                table: "learning_path_items",
                columns: new[] { "center_id", "topic_node_id" });

            migrationBuilder.CreateIndex(
                name: "ux_learning_path_items_center_id_learning_path_id_rank_order",
                table: "learning_path_items",
                columns: new[] { "center_id", "learning_path_id", "rank_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_learning_path_items_center_id_learning_path_id_topic_node_id",
                table: "learning_path_items",
                columns: new[] { "center_id", "learning_path_id", "topic_node_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_learning_paths_center_id_student_id_subject_id_status",
                table: "learning_paths",
                columns: new[] { "center_id", "student_id", "subject_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_paths_center_id_subject_id",
                table: "learning_paths",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_center_id_question_id",
                table: "recommendations",
                columns: new[] { "center_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_center_id_subject_id",
                table: "recommendations",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_center_id_topic_node_id",
                table: "recommendations",
                columns: new[] { "center_id", "topic_node_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_center_student_subject_status_generated_at",
                table: "recommendations",
                columns: new[] { "center_id", "student_id", "subject_id", "status", "generated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_student_subject_goals_center_id_subject_id_risk_score",
                table: "student_subject_goals",
                columns: new[] { "center_id", "subject_id", "risk_score" });

            migrationBuilder.CreateIndex(
                name: "ux_student_subject_goals_center_id_student_id_subject_id",
                table: "student_subject_goals",
                columns: new[] { "center_id", "student_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_student_twins_center_id_student_id",
                table: "student_twins",
                columns: new[] { "center_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_twin_update_history_center_id_attempt_id",
                table: "twin_update_history",
                columns: new[] { "center_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_twin_update_history_center_id_subject_id",
                table: "twin_update_history",
                columns: new[] { "center_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_twin_update_history_center_id_topic_node_id_created_at",
                table: "twin_update_history",
                columns: new[] { "center_id", "topic_node_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_twin_update_history_center_student_subject_created_at",
                table: "twin_update_history",
                columns: new[] { "center_id", "student_id", "subject_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "behavior_twins");

            migrationBuilder.DropTable(
                name: "knowledge_twins");

            migrationBuilder.DropTable(
                name: "learning_path_items");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "student_subject_goals");

            migrationBuilder.DropTable(
                name: "student_twins");

            migrationBuilder.DropTable(
                name: "twin_update_history");

            migrationBuilder.DropTable(
                name: "learning_paths");
        }
    }
}
