using System.Reflection;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace EduTwin.BLL.Tests.Persistence;

public sealed class AddAiSolutionToReasoningAnalysisMigrationTests
{
    [Fact]
    public void Migration_HasDiscoveryMetadata()
    {
        var migrationType = typeof(AddAiSolutionToReasoningAnalysis);
        var migrationAttribute = migrationType.GetCustomAttribute<MigrationAttribute>();
        var dbContextAttribute = migrationType.GetCustomAttribute<DbContextAttribute>();

        migrationAttribute.Should().NotBeNull();
        migrationAttribute!.Id.Should().Be("20260920153000_AddAiSolutionToReasoningAnalysis");
        dbContextAttribute.Should().NotBeNull();
        dbContextAttribute!.ContextType.Should().Be(typeof(EduTwinDbContext));
    }

    [Fact]
    public void Up_AddsAiSolutionColumnsAndConstraint()
    {
        var migration = new TestableMigration();
        var migrationBuilder = new MigrationBuilder("MySQL");

        migration.ApplyUp(migrationBuilder);

        migrationBuilder.Operations.OfType<AddColumnOperation>()
            .Select(operation => operation.Name)
            .Should().BeEquivalentTo("solution_type", "ai_solution");
        migrationBuilder.Operations.OfType<AddCheckConstraintOperation>()
            .Should().ContainSingle(operation =>
                operation.Name == "ck_reasoning_analyses_solution_type" &&
                operation.Table == "reasoning_analyses");
    }

    private sealed class TestableMigration : AddAiSolutionToReasoningAnalysis
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
