using System;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EduTwinDbContext))]
    [Migration("20260920103000_AddCoreFlowAndReviewEnhancements")]
    public partial class AddCoreFlowAndReviewEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "manual_retry_count",
                table: "attempts",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_manual_retry_at",
                table: "attempts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "solution_exposed_at",
                table: "attempts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_post_feedback",
                table: "attempts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "misconception",
                table: "question_options",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "student_review_requests",
                columns: table => new
                {
                    request_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySql.EntityFrameworkCore.Metadata.MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    question_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    student_comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    teacher_note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    resolved_by_teacher_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_review_requests", x => x.request_id);
                    table.UniqueConstraint("ux_student_review_requests_center_id_request_id", x => new { x.center_id, x.request_id });
                    table.ForeignKey(
                        name: "fk_student_review_requests_attempts",
                        columns: x => new { x.center_id, x.attempt_id },
                        principalTable: "attempts",
                        principalColumns: new[] { "center_id", "attempt_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_student_review_requests_questions",
                        columns: x => new { x.center_id, x.question_id },
                        principalTable: "questions",
                        principalColumns: new[] { "center_id", "question_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_review_requests_students",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_review_requests_teachers",
                        columns: x => new { x.center_id, x.resolved_by_teacher_id },
                        principalTable: "teachers",
                        principalColumns: new[] { "center_id", "teacher_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_review_requests_center_id_attempt_id",
                table: "student_review_requests",
                columns: new[] { "center_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_student_review_requests_center_id_student_id_status",
                table: "student_review_requests",
                columns: new[] { "center_id", "student_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_review_requests");

            migrationBuilder.DropColumn(
                name: "misconception",
                table: "question_options");

            migrationBuilder.DropColumn(
                name: "is_post_feedback",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "solution_exposed_at",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "last_manual_retry_at",
                table: "attempts");

            migrationBuilder.DropColumn(
                name: "manual_retry_count",
                table: "attempts");
        }
    }
}
