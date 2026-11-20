using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EduTwin.DAL.Persistence.Migrations;

[DbContext(typeof(EduTwinDbContext))]
[Migration("20261120160000_CorrectSeedQuadraticSignRule")]
public sealed class CorrectSeedQuadraticSignRule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop and restore the knowledge_nodes update guards around this one-time seed wording fix;
        // published curricula already contain MATH10-QUADRATIC, so the triggers reject direct description edits.
        HardenAcademicLifecycleDependencies.DropKnowledgeNodeLifecycleTrigger(migrationBuilder);
        KnowledgeGradeScopeMigrationSql.DropCanonicalNodeGraphFreezeTrigger(migrationBuilder);

        // Correct only the known seed wording, preserving teacher-authored replacements.
        migrationBuilder.Sql("""
            UPDATE knowledge_nodes
            SET description = REPLACE(description,
                'ngoài khoảng hai nghiệm trái dấu với a, trong khoảng hai nghiệm cùng dấu với a',
                'khi Δ > 0, ngoài khoảng hai nghiệm cùng dấu với a, trong khoảng hai nghiệm trái dấu với a')
            WHERE node_code = 'MATH10-QUADRATIC'
              AND description LIKE '%ngoài khoảng hai nghiệm trái dấu với a, trong khoảng hai nghiệm cùng dấu với a%';
            """);

        HardenAcademicLifecycleDependencies.AddKnowledgeNodeLifecycleTrigger(migrationBuilder);
        KnowledgeGradeScopeMigrationSql.AddCanonicalNodeGraphFreezeTrigger(migrationBuilder);
    }

    // A rollback must not reintroduce incorrect teaching content.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
