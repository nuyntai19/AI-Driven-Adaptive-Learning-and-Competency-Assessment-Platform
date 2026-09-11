using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenRecommendationGenerationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dismiss_reason",
                table: "recommendations",
                type: "varchar(1000)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recommendation_generation_states",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    student_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    last_trigger_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_source_attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    last_outcome = table.Column<string>(type: "varchar(32)", nullable: false),
                    diagnostic_reason = table.Column<string>(type: "varchar(500)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_generation_states", x => new { x.center_id, x.student_id, x.subject_id });
                    table.CheckConstraint("ck_recommendation_generation_states_outcome", "`last_outcome` IN ('Generated', 'NoCandidate', 'Blocked')");
                    table.ForeignKey(
                        name: "fk_recommendation_generation_states_students_student",
                        columns: x => new { x.center_id, x.student_id },
                        principalTable: "students",
                        principalColumns: new[] { "center_id", "student_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recommendation_generation_states_subjects_subject",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_generation_states_center_id_subject_id",
                table: "recommendation_generation_states",
                columns: new[] { "center_id", "subject_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recommendation_generation_states");

            migrationBuilder.DropColumn(
                name: "dismiss_reason",
                table: "recommendations");
        }
    }
}
