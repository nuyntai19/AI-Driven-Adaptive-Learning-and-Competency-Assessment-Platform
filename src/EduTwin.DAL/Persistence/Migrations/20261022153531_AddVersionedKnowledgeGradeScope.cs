using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTwin.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedKnowledgeGradeScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "graph_version_id",
                table: "curriculums",
                type: "varchar(36)",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_knowledge_edges_center_id_edge_id",
                table: "knowledge_edges",
                columns: new[] { "center_id", "edge_id" });

            migrationBuilder.CreateTable(
                name: "knowledge_graph_versions",
                columns: table => new
                {
                    graph_version_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    subject_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    grade_level = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    status = table.Column<string>(type: "varchar(16)", nullable: false),
                    is_current = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_legacy = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    title = table.Column<string>(type: "varchar(250)", nullable: true),
                    source_graph_version_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<string>(type: "varchar(36)", nullable: true),
                    row_version = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 1ul),
                    current_grade = table.Column<byte>(type: "tinyint unsigned", nullable: true, computedColumnSql: "CASE WHEN is_current=1 THEN grade_level ELSE NULL END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_graph_versions", x => x.graph_version_id);
                    table.UniqueConstraint("AK_knowledge_graph_versions_center_id_graph_version_id", x => new { x.center_id, x.graph_version_id });
                    table.CheckConstraint("ck_graph_versions_current", "is_current=0 OR (status='Published' AND is_legacy=0)");
                    table.CheckConstraint("ck_graph_versions_grade", "grade_level BETWEEN 10 AND 12");
                    table.CheckConstraint("ck_graph_versions_status", "status IN ('Draft','Published','Archived')");
                    table.ForeignKey(
                        name: "FK_knowledge_graph_versions_knowledge_graph_versions_center_id_~",
                        columns: x => new { x.center_id, x.source_graph_version_id },
                        principalTable: "knowledge_graph_versions",
                        principalColumns: new[] { "center_id", "graph_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_graph_versions_subjects_center_id_subject_id",
                        columns: x => new { x.center_id, x.subject_id },
                        principalTable: "subjects",
                        principalColumns: new[] { "center_id", "subject_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "knowledge_edge_grades",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    graph_version_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    edge_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    relation_type = table.Column<string>(type: "varchar(32)", nullable: false),
                    weight = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    source_graph_version_id = table.Column<string>(type: "varchar(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_edge_grades", x => new { x.center_id, x.graph_version_id, x.edge_id });
                    table.CheckConstraint("ck_edge_grades_relation", "relation_type IN ('PrerequisiteOf','RelatedTo','PartOf','CausesErrorIn')");
                    table.CheckConstraint("ck_edge_grades_weight", "weight BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_knowledge_edge_grades_knowledge_edges_center_id_edge_id",
                        columns: x => new { x.center_id, x.edge_id },
                        principalTable: "knowledge_edges",
                        principalColumns: new[] { "center_id", "edge_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_edge_grades_knowledge_graph_versions_center_id_gra~",
                        columns: x => new { x.center_id, x.graph_version_id },
                        principalTable: "knowledge_graph_versions",
                        principalColumns: new[] { "center_id", "graph_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_edge_grades_knowledge_graph_versions_center_id_sou~",
                        columns: x => new { x.center_id, x.source_graph_version_id },
                        principalTable: "knowledge_graph_versions",
                        principalColumns: new[] { "center_id", "graph_version_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "knowledge_node_grades",
                columns: table => new
                {
                    center_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    graph_version_id = table.Column<string>(type: "varchar(36)", nullable: false),
                    node_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    parent_node_id = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<string>(type: "varchar(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_node_grades", x => new { x.center_id, x.graph_version_id, x.node_id });
                    table.CheckConstraint("ck_node_grades_parent", "parent_node_id IS NULL OR parent_node_id<>node_id");
                    table.ForeignKey(
                        name: "FK_knowledge_node_grades_knowledge_graph_versions_center_id_gra~",
                        columns: x => new { x.center_id, x.graph_version_id },
                        principalTable: "knowledge_graph_versions",
                        principalColumns: new[] { "center_id", "graph_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_node_grades_knowledge_node_grades_center_id_graph_~",
                        columns: x => new { x.center_id, x.graph_version_id, x.parent_node_id },
                        principalTable: "knowledge_node_grades",
                        principalColumns: new[] { "center_id", "graph_version_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_node_grades_knowledge_nodes_center_id_node_id",
                        columns: x => new { x.center_id, x.node_id },
                        principalTable: "knowledge_nodes",
                        principalColumns: new[] { "center_id", "node_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_curriculums_center_id_graph_version_id",
                table: "curriculums",
                columns: new[] { "center_id", "graph_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_edge_grades_center_id_edge_id",
                table: "knowledge_edge_grades",
                columns: new[] { "center_id", "edge_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_edge_grades_center_id_source_graph_version_id",
                table: "knowledge_edge_grades",
                columns: new[] { "center_id", "source_graph_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_graph_versions_center_id_source_graph_version_id",
                table: "knowledge_graph_versions",
                columns: new[] { "center_id", "source_graph_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_graph_versions_center_id_subject_id_grade_level_st~",
                table: "knowledge_graph_versions",
                columns: new[] { "center_id", "subject_id", "grade_level", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_graph_versions_current_grade",
                table: "knowledge_graph_versions",
                columns: new[] { "center_id", "subject_id", "current_grade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_node_grades_center_id_graph_version_id_parent_node~",
                table: "knowledge_node_grades",
                columns: new[] { "center_id", "graph_version_id", "parent_node_id" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_node_grades_center_id_node_id_graph_version_id",
                table: "knowledge_node_grades",
                columns: new[] { "center_id", "node_id", "graph_version_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_curriculums_graph_versions",
                table: "curriculums",
                columns: new[] { "center_id", "graph_version_id" },
                principalTable: "knowledge_graph_versions",
                principalColumns: new[] { "center_id", "graph_version_id" },
                onDelete: ReferentialAction.Restrict);

            KnowledgeGradeScopeMigrationSql.Backfill(migrationBuilder);
            KnowledgeGradeScopeMigrationSql.AddGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            KnowledgeGradeScopeMigrationSql.DropGuards(migrationBuilder);
            migrationBuilder.DropForeignKey(
                name: "fk_curriculums_graph_versions",
                table: "curriculums");

            migrationBuilder.DropTable(
                name: "knowledge_edge_grades");

            migrationBuilder.DropTable(
                name: "knowledge_node_grades");

            migrationBuilder.DropTable(
                name: "knowledge_graph_versions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_knowledge_edges_center_id_edge_id",
                table: "knowledge_edges");

            migrationBuilder.DropIndex(
                name: "IX_curriculums_center_id_graph_version_id",
                table: "curriculums");

            migrationBuilder.DropColumn(
                name: "graph_version_id",
                table: "curriculums");
        }
    }
}
