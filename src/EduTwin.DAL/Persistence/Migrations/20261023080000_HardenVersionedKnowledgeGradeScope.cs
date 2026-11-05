using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EduTwin.DAL.Persistence.Migrations;

/// <summary>Forward repair for installations that already applied the additive grade-scope schema.</summary>
[DbContext(typeof(EduTwinDbContext))]
[Migration("20261023080000_HardenVersionedKnowledgeGradeScope")]
public sealed class HardenVersionedKnowledgeGradeScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        KnowledgeGradeScopeMigrationSql.DropGuards(migrationBuilder);
        KnowledgeGradeScopeMigrationSql.AddGuards(migrationBuilder);
    }

    // Application rollback keeps these data-protection guards and all scope/history data intact.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
