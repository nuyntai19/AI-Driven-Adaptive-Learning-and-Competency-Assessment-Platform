using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace EduTwin.BLL.Tests.Persistence;

public sealed class MissingModulePersistenceMigrationTests
{
    [Fact]
    public void Migration_IsDiscoveredAndCurrentModelMatchesSnapshot()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL("Server=dummy;Database=dummy").Options;
        using var db = new EduTwinDbContext(options);
        var migrations = db.GetService<IMigrationsAssembly>();
        Assert.Contains("20261105174213_RestoreMissingModulePersistence", migrations.Migrations.Keys);
        Assert.Contains("20261013010000_AddTeacherStudentNotes", migrations.Migrations.Keys);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Migration_CreatesMissingModuleTablesWithoutRewritingExistingData()
    {
        var migration = new RestoreMissingModulePersistence();
        var operations = migration.UpOperations;
        var tables = operations.OfType<CreateTableOperation>().ToList();
        Assert.Equal(new[] { "adaptive_practice_session_items", "adaptive_practice_session_summaries",
            "adaptive_practice_sessions" }, tables.Select(t => t.Name).Order().ToArray());
        Assert.All(tables, table => Assert.Contains(table.Columns, c => c.Name == "center_id" && !c.IsNullable));
        Assert.All(operations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        var session = tables.Single(t => t.Name == "adaptive_practice_sessions");
        foreach (var principal in new[] { "students", "subjects", "knowledge_nodes" })
            Assert.Contains(session.ForeignKeys, fk => fk.PrincipalTable == principal && fk.Columns.Contains("center_id"));
    }
}
