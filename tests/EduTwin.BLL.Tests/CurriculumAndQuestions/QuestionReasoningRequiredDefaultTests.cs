using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using MySql.Data.MySqlClient;
using Xunit;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class QuestionReasoningRequiredDefaultTests
{
    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING"))
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Default")))
            {
                Skip = "Set EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING or ConnectionStrings__Default to run MySQL integration tests.";
            }
        }
    }

    [Fact]
    public void EF_Model_HasDefaultValue_False_For_ReasoningRequired()
    {
        var tenantMock = new Mock<ITenantIdAccessor>();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new EduTwinDbContext(options, tenantMock.Object);
        var questionEntity = dbContext.Model.FindEntityType(typeof(Question));

        Assert.NotNull(questionEntity);
        var reasoningProp = questionEntity.FindProperty(nameof(Question.ReasoningRequired));
        Assert.NotNull(reasoningProp);
        Assert.Equal(false, reasoningProp.GetDefaultValue());
    }

    [Fact]
    public void ModelSnapshot_Specifies_HasDefaultValue_False()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var snapshotPath = Path.Combine(basePath, "../../../../../src/EduTwin.DAL/Persistence/Migrations/EduTwinDbContextModelSnapshot.cs");
        Assert.True(File.Exists(snapshotPath), $"Snapshot file was not found: {snapshotPath}");

        var content = File.ReadAllText(snapshotPath);
        Assert.Contains(".HasColumnName(\"reasoning_required\")", content);
        Assert.Contains(".HasDefaultValue(false)", content);
    }

    [MySqlIntegrationFact]
    public async Task MySql_Database_Column_Default_Is_False_When_Available()
    {
        var connectionString = Environment.GetEnvironmentVariable("EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")!;

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COLUMN_DEFAULT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'questions'
  AND COLUMN_NAME = 'reasoning_required';";

        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        // MySQL stores boolean default as '0' or 'b'0''
        var defaultValue = result.ToString();
        Assert.True(defaultValue == "0" || defaultValue == "b'0'" || defaultValue == "FALSE",
            $"Expected reasoning_required column default to be '0' or false, but got '{defaultValue}'.");
    }
}
