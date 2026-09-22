using System;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EduTwinDbContext))]
    [Migration("20260920190000_AddStudentLearningPathAndReviewWorkflow")]
    public partial class AddStudentLearningPathAndReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add review fields to reasoning_analyses
            migrationBuilder.AddColumn<string>(
                name: "review_decision",
                table: "reasoning_analyses",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reviewed_by_user_id",
                table: "reasoning_analyses",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "reasoning_analyses",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teacher_review_note",
                table: "reasoning_analyses",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reasoning_analyses_review_decision",
                table: "reasoning_analyses",
                sql: "`review_decision` IS NULL OR `review_decision` IN ('Approved', 'Adjusted')");

            migrationBuilder.CreateIndex(
                name: "ix_reasoning_analyses_center_id_reviewed_by_user_id",
                table: "reasoning_analyses",
                columns: new[] { "center_id", "reviewed_by_user_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_reasoning_analyses_users_reviewed_by_user",
                table: "reasoning_analyses",
                columns: new[] { "center_id", "reviewed_by_user_id" },
                principalTable: "users",
                principalColumns: new[] { "center_id", "user_id" },
                onDelete: ReferentialAction.Restrict);

            // 2. Add overall AI comment fields to student_assignment_progress
            migrationBuilder.AddColumn<string>(
                name: "overall_ai_comment",
                table: "student_assignment_progress",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "overall_ai_comment_generated_at",
                table: "student_assignment_progress",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "overall_ai_comment_version",
                table: "student_assignment_progress",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<bool>(
                name: "is_overall_ai_comment_stale",
                table: "student_assignment_progress",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // 3. Create teacher_review_histories table (audit trail)
            migrationBuilder.CreateTable(
                name: "teacher_review_histories",
                columns: table => new
                {
                    history_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySql.EntityFrameworkCore.Metadata.MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    analysis_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    teacher_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    decision = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    previous_score = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    new_score = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    previous_is_correct = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    new_is_correct = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    override_version = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_review_histories", x => x.history_id);
                    table.UniqueConstraint("ux_teacher_review_histories_center_id_history_id", x => new { x.center_id, x.history_id });
                    table.CheckConstraint("ck_teacher_review_histories_decision", "`decision` IN ('Approved', 'Adjusted')");
                    table.ForeignKey(
                        name: "fk_teacher_review_histories_reasoning_analyses",
                        columns: x => new { x.center_id, x.analysis_id },
                        principalTable: "reasoning_analyses",
                        principalColumns: new[] { "center_id", "analysis_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_review_histories_attempts",
                        columns: x => new { x.center_id, x.attempt_id },
                        principalTable: "attempts",
                        principalColumns: new[] { "center_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_review_histories_teachers",
                        columns: x => new { x.center_id, x.teacher_id },
                        principalTable: "users",
                        principalColumns: new[] { "center_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_teacher_review_histories_center_id_analysis_id",
                table: "teacher_review_histories",
                columns: new[] { "center_id", "analysis_id" });

            migrationBuilder.CreateIndex(
                name: "ix_teacher_review_histories_center_id_attempt_id",
                table: "teacher_review_histories",
                columns: new[] { "center_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_teacher_review_histories_center_id_teacher_id",
                table: "teacher_review_histories",
                columns: new[] { "center_id", "teacher_id" });

            // 4. Create student_learning_path_preferences table
            migrationBuilder.CreateTable(
                name: "student_learning_path_preferences",
                columns: table => new
                {
                    preference_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySql.EntityFrameworkCore.Metadata.MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    self_assessed_level = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    weak_topic_node_ids = table.Column<string>(type: "json", nullable: false),
                    focus_topic_node_ids = table.Column<string>(type: "json", nullable: false),
                    goal_type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    target_mastery = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    target_weeks = table.Column<int>(type: "int", nullable: false),
                    minutes_per_day = table.Column<int>(type: "int", nullable: false),
                    days_per_week = table.Column<int>(type: "int", nullable: false),
                    pace = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    preferred_mode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    deleted_by = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_learning_path_preferences", x => x.preference_id);
                    table.UniqueConstraint("ux_slp_pref_center_pref_id", x => new { x.center_id, x.preference_id });
                    table.ForeignKey(
                        name: "fk_student_learning_path_preferences_students",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_learning_path_preferences_subjects",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_slp_pref_center_student_subject",
                table: "student_learning_path_preferences",
                columns: new[] { "center_id", "student_id", "subject_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "student_learning_path_preferences");
            migrationBuilder.DropTable(name: "teacher_review_histories");

            migrationBuilder.DropColumn(name: "is_overall_ai_comment_stale", table: "student_assignment_progress");
            migrationBuilder.DropColumn(name: "overall_ai_comment_version", table: "student_assignment_progress");
            migrationBuilder.DropColumn(name: "overall_ai_comment_generated_at", table: "student_assignment_progress");
            migrationBuilder.DropColumn(name: "overall_ai_comment", table: "student_assignment_progress");

            migrationBuilder.DropForeignKey(name: "fk_reasoning_analyses_users_reviewed_by_user", table: "reasoning_analyses");
            migrationBuilder.DropIndex(name: "ix_reasoning_analyses_center_id_reviewed_by_user_id", table: "reasoning_analyses");
            migrationBuilder.DropCheckConstraint(name: "ck_reasoning_analyses_review_decision", table: "reasoning_analyses");

            migrationBuilder.DropColumn(name: "teacher_review_note", table: "reasoning_analyses");
            migrationBuilder.DropColumn(name: "reviewed_at", table: "reasoning_analyses");
            migrationBuilder.DropColumn(name: "reviewed_by_user_id", table: "reasoning_analyses");
            migrationBuilder.DropColumn(name: "review_decision", table: "reasoning_analyses");
        }
    }
}
