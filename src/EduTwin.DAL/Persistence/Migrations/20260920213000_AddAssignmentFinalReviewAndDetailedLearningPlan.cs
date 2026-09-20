using System;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations;

[DbContext(typeof(EduTwinDbContext))]
[Migration("20260920213000_AddAssignmentFinalReviewAndDetailedLearningPlan")]
public partial class AddAssignmentFinalReviewAndDetailedLearningPlan : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("teacher_final_review_status", "student_assignment_progress", "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Pending");
        migrationBuilder.AddColumn<string>("final_reviewed_by_user_id", "student_assignment_progress", "varchar(36)", maxLength: 36, nullable: true);
        migrationBuilder.AddColumn<DateTime>("final_reviewed_at", "student_assignment_progress", "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<string>("final_teacher_note", "student_assignment_progress", "varchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<uint>("final_review_version", "student_assignment_progress", "int unsigned", nullable: false, defaultValue: 0u);
        migrationBuilder.AddCheckConstraint("ck_student_assignment_progress_final_review", "student_assignment_progress", "`teacher_final_review_status` IN ('Pending', 'Approved')");
        migrationBuilder.CreateIndex("ix_student_assignment_progress_center_final_reviewer", "student_assignment_progress", new[] { "center_id", "final_reviewed_by_user_id" });
        migrationBuilder.AddForeignKey("fk_student_assignment_progress_users_final_reviewer", "student_assignment_progress",
            new[] { "center_id", "final_reviewed_by_user_id" }, "users", new[] { "center_id", "user_id" }, onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddColumn<string>("plan_json", "learning_paths", "json", nullable: true);
        migrationBuilder.AddColumn<string>("recommendation_rationale", "learning_paths", "longtext", nullable: true);
        migrationBuilder.AddColumn<string>("plan_schema_version", "learning_paths", "varchar(16)", maxLength: 16, nullable: false, defaultValue: "2.0");
        migrationBuilder.AddColumn<string>("generation_status", "learning_paths", "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Ready");
        migrationBuilder.AddColumn<string>("adaptation_message", "learning_paths", "longtext", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("adaptation_message", "learning_paths");
        migrationBuilder.DropColumn("generation_status", "learning_paths");
        migrationBuilder.DropColumn("plan_schema_version", "learning_paths");
        migrationBuilder.DropColumn("recommendation_rationale", "learning_paths");
        migrationBuilder.DropColumn("plan_json", "learning_paths");
        migrationBuilder.DropForeignKey("fk_student_assignment_progress_users_final_reviewer", "student_assignment_progress");
        migrationBuilder.DropIndex("ix_student_assignment_progress_center_final_reviewer", "student_assignment_progress");
        migrationBuilder.DropCheckConstraint("ck_student_assignment_progress_final_review", "student_assignment_progress");
        migrationBuilder.DropColumn("final_review_version", "student_assignment_progress");
        migrationBuilder.DropColumn("final_teacher_note", "student_assignment_progress");
        migrationBuilder.DropColumn("final_reviewed_at", "student_assignment_progress");
        migrationBuilder.DropColumn("final_reviewed_by_user_id", "student_assignment_progress");
        migrationBuilder.DropColumn("teacher_final_review_status", "student_assignment_progress");
    }
}
