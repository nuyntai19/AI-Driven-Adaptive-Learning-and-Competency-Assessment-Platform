using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningPathCalendarAndExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "planned_date",
                table: "learning_path_sessions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "schedule_settings",
                table: "learning_goals",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "start_with_placement",
                table: "learning_goals",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "target_score",
                table: "learning_goals",
                type: "decimal(4,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assignment_kind",
                table: "assignments",
                type: "varchar(24)",
                nullable: false,
                defaultValue: "Regular");

            migrationBuilder.AddColumn<DateTime>(
                name: "exam_at",
                table: "assignments",
                type: "datetime(6)",
                nullable: true);
            // Retain the former Twin input as provenance. Accepted route deadlines win conflicts; scores are moved into the canonical objective.
            migrationBuilder.Sql("""
                UPDATE learning_goals g JOIN student_subject_goals t
                  ON t.center_id = g.center_id AND t.student_id = g.student_id AND t.subject_id = g.subject_id AND t.is_deleted = 0
                SET g.target_score = t.target_score,
                    g.deadline_date = COALESCE(g.deadline_date, DATE_ADD(DATE_ADD(DATE(t.updated_at + INTERVAL 7 HOUR), INTERVAL t.remaining_days DAY), INTERVAL 17 HOUR) - INTERVAL 1 SECOND),
                    g.schedule_settings = JSON_OBJECT('timeZoneId', 'Asia/Ho_Chi_Minh', 'legacyTwinGoal',
                      JSON_OBJECT('goalId', CAST(t.goal_id AS CHAR), 'targetScore', t.target_score, 'remainingDays', t.remaining_days, 'updatedAt', t.updated_at))
                WHERE g.status = 'Active' AND g.is_deleted = 0;

                INSERT INTO learning_goals
                  (goal_id, center_id, student_id, subject_id, goal_type, title, target_skill_ids, deadline_date, target_score,
                   start_with_placement, schedule_settings, available_minutes_per_day, available_days_per_week,
                   preferred_pace, preferred_mode, status, created_at, updated_at, is_deleted, row_version)
                SELECT UUID(), t.center_id, t.student_id, t.subject_id, 'ExamPrep', 'Mục tiêu môn học', JSON_ARRAY(),
                  DATE_ADD(DATE_ADD(DATE(t.updated_at + INTERVAL 7 HOUR), INTERVAL t.remaining_days DAY), INTERVAL 17 HOUR) - INTERVAL 1 SECOND,
                  t.target_score, 0, JSON_OBJECT('timeZoneId', 'Asia/Ho_Chi_Minh', 'legacyTwinGoal',
                    JSON_OBJECT('goalId', CAST(t.goal_id AS CHAR), 'targetScore', t.target_score, 'remainingDays', t.remaining_days, 'updatedAt', t.updated_at)),
                  15, 5, 'Moderate', 'Balanced', 'Active', t.created_at, t.updated_at, 0, 1
                FROM student_subject_goals t
                WHERE t.is_deleted = 0 AND NOT EXISTS
                  (SELECT 1 FROM learning_goals g WHERE g.center_id = t.center_id AND g.student_id = t.student_id
                   AND g.subject_id = t.subject_id AND g.is_deleted = 0 AND g.status IN ('Active', 'Paused', 'Achieved'));

                UPDATE learning_goals SET start_with_placement = 1, goal_type = 'KeepUp' WHERE goal_type = 'Placement';

                INSERT INTO learning_goals
                  (goal_id, center_id, student_id, subject_id, goal_type, title, target_skill_ids, deadline_date, target_score,
                   start_with_placement, schedule_settings, available_minutes_per_day, available_days_per_week,
                   preferred_pace, preferred_mode, status, created_at, updated_at, is_deleted, row_version)
                SELECT UUID(), p.center_id, p.student_id, p.subject_id,
                  CASE WHEN p.goal_type IN ('Foundation', 'KeepUp', 'Catchup', 'ImproveGrade', 'ExamPrep', 'Advanced', 'Diagnostic') THEN p.goal_type ELSE 'KeepUp' END,
                  'Mục tiêu môn học', JSON_ARRAY(), DATE_ADD(p.updated_at, INTERVAL (p.target_weeks * 7) DAY), p.target_mastery / 10,
                  0, JSON_OBJECT('timeZoneId', 'Asia/Ho_Chi_Minh', 'legacyPreferenceId', CAST(p.preference_id AS CHAR)),
                  LEAST(300, GREATEST(5, p.minutes_per_day)), LEAST(7, GREATEST(1, p.days_per_week)),
                  CASE WHEN p.pace IN ('Gentle', 'Moderate', 'Accelerated') THEN p.pace ELSE 'Moderate' END,
                  p.preferred_mode, 'Active', p.created_at, p.updated_at, 0, 1
                FROM student_learning_path_preferences p
                WHERE p.is_deleted = 0 AND NOT EXISTS
                  (SELECT 1 FROM learning_goals g WHERE g.center_id = p.center_id AND g.student_id = p.student_id
                   AND g.subject_id = p.subject_id AND g.is_deleted = 0 AND g.status IN ('Active', 'Paused', 'Achieved'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE learning_goals SET goal_type = 'Placement' WHERE start_with_placement = 1 AND goal_type = 'KeepUp';");
            migrationBuilder.DropColumn(
                name: "planned_date",
                table: "learning_path_sessions");

            migrationBuilder.DropColumn(
                name: "schedule_settings",
                table: "learning_goals");

            migrationBuilder.DropColumn(
                name: "start_with_placement",
                table: "learning_goals");

            migrationBuilder.DropColumn(
                name: "target_score",
                table: "learning_goals");

            migrationBuilder.DropColumn(
                name: "assignment_kind",
                table: "assignments");

            migrationBuilder.DropColumn(
                name: "exam_at",
                table: "assignments");
        }
    }
}
