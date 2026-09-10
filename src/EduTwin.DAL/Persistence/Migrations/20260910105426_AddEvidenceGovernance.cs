using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ux_reasoning_analyses_center_id_analysis_id_attempt_id",
                table: "reasoning_analyses",
                columns: new[] { "center_id", "analysis_id", "attempt_id" });

            migrationBuilder.CreateTable(
                name: "evidence_assessments",
                columns: table => new
                {
                    evidence_assessment_id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    attempt_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    analysis_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    supersedes_assessment_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    source_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    trust_level = table.Column<string>(type: "varchar(32)", nullable: false),
                    decision_mode = table.Column<string>(type: "varchar(32)", nullable: false),
                    reasoning_weight = table.Column<decimal>(type: "decimal(4,3)", nullable: false),
                    reason_codes = table.Column<string>(type: "json", nullable: false),
                    requires_teacher_review = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    policy_version = table.Column<string>(type: "varchar(32)", nullable: false),
                    analysis_override_version = table.Column<uint>(type: "int unsigned", nullable: false),
                    evaluated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_assessments", x => x.evidence_assessment_id);
                    table.UniqueConstraint("ux_evidence_center_assessment", x => new { x.center_id, x.evidence_assessment_id });
                    table.UniqueConstraint("ux_evidence_center_assessment_attempt", x => new { x.center_id, x.evidence_assessment_id, x.attempt_id });
                    table.CheckConstraint("ck_evidence_assessments_decision_mode", "`decision_mode` IN ('AIWeighted', 'DeterministicOnly', 'HumanConfirmed')");
                    table.CheckConstraint("ck_evidence_assessments_reasoning_weight", "`reasoning_weight` BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_evidence_assessments_source_type", "`source_type` IN ('AI', 'RuleFallback', 'TeacherOverride')");
                    table.CheckConstraint("ck_evidence_assessments_trust_level", "`trust_level` IN ('Trusted', 'Reduced', 'ReviewOnly')");
                    table.ForeignKey(
                        name: "fk_evidence_assessments_attempts_attempt",
                        columns: x => new { x.center_id, x.attempt_id },
                        principalTable: "attempts",
                        principalColumns: new[] { "center_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidence_assessments_evidence_assessments_supersedes",
                        columns: x => new { x.center_id, x.supersedes_assessment_id, x.attempt_id },
                        principalTable: "evidence_assessments",
                        principalColumns: new[] { "center_id", "evidence_assessment_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidence_assessments_reasoning_analyses_analysis",
                        columns: x => new { x.center_id, x.analysis_id, x.attempt_id },
                        principalTable: "reasoning_analyses",
                        principalColumns: new[] { "center_id", "analysis_id", "attempt_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_center_analysis_attempt",
                table: "evidence_assessments",
                columns: new[] { "center_id", "analysis_id", "attempt_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_center_attempt_evaluated",
                table: "evidence_assessments",
                columns: new[] { "center_id", "attempt_id", "evaluated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_center_review_evaluated",
                table: "evidence_assessments",
                columns: new[] { "center_id", "requires_teacher_review", "evaluated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_center_supersedes_attempt",
                table: "evidence_assessments",
                columns: new[] { "center_id", "supersedes_assessment_id", "attempt_id" });

            migrationBuilder.Sql(
                """
                INSERT INTO evidence_assessments
                (
                    center_id,
                    attempt_id,
                    analysis_id,
                    supersedes_assessment_id,
                    source_type,
                    trust_level,
                    decision_mode,
                    reasoning_weight,
                    reason_codes,
                    requires_teacher_review,
                    policy_version,
                    analysis_override_version,
                    evaluated_at,
                    created_at,
                    created_by
                )
                SELECT
                    center_id,
                    attempt_id,
                    analysis_id,
                    NULL,
                    CASE
                        WHEN override_version > 0 THEN 'TeacherOverride'
                        WHEN is_fallback = 1 THEN 'RuleFallback'
                        ELSE 'AI'
                    END,
                    'ReviewOnly',
                    CASE
                        WHEN override_version > 0 THEN 'HumanConfirmed'
                        WHEN is_fallback = 1 THEN 'DeterministicOnly'
                        ELSE 'AIWeighted'
                    END,
                    0.000,
                    JSON_ARRAY('HISTORICAL_BACKFILL_REVIEW_ONLY'),
                    1,
                    'evidence-gate-v1',
                    override_version,
                    COALESCE(overridden_at, updated_at, created_at),
                    COALESCE(overridden_at, updated_at, created_at),
                    CASE
                        WHEN override_version > 0 THEN overridden_by_teacher_id
                        ELSE NULL
                    END
                FROM reasoning_analyses;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_assessments");

            migrationBuilder.DropUniqueConstraint(
                name: "ux_reasoning_analyses_center_id_analysis_id_attempt_id",
                table: "reasoning_analyses");
        }
    }
}
