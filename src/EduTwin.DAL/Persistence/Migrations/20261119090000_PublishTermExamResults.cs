using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EduTwin.DAL.Persistence.Migrations;

[DbContext(typeof(EduTwinDbContext))]
[Migration("20261119090000_PublishTermExamResults")]
public sealed class PublishTermExamResults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<DateTime>(
            name: "results_published_at", table: "assignments", type: "datetime(6)", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "results_published_at", table: "assignments");
}
