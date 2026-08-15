using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySql.Data.MySqlClient;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed class AIAnalysisJobProcessorMySqlTests
{
    private const string AdminConnectionVariable =
        "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime UtcNow =
        new(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc);

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_FailureAfterRelationalSave_RollsBackAllAggregates()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, centerId);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using (var context = CreateContext(
                         database.ConnectionString,
                         tenant,
                         new ThrowAfterSaveInterceptor()))
        {
            var processor = CreateProcessor(context, tenant);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                processor.ExecuteAsync(1, "mysql-worker", CancellationToken.None));
        }

        var persisted = await ReloadAsync(database.ConnectionString, centerId);
        Assert.Equal(AttemptStatus.PendingAnalysis, persisted.Attempt.Status);
        Assert.Equal(1ul, persisted.Attempt.RowVersion);
        Assert.Equal(AIJobStatus.Processing, persisted.Job.Status);
        Assert.Equal(1ul, persisted.Job.RowVersion);
        Assert.Empty(persisted.Analyses);
    }

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_TwoRelationalTransactions_OnlyOneProcessorWins()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, centerId);
        var barrier = new CompetingSaveBarrier(2);
        var tenantA = new TenantContext();
        var tenantB = new TenantContext();
        using var tenantScopeA = tenantA.BeginScope(centerId);
        using var tenantScopeB = tenantB.BeginScope(centerId);
        await using (var contextA = CreateContext(
                         database.ConnectionString,
                         tenantA,
                         barrier))
        await using (var contextB = CreateContext(
                         database.ConnectionString,
                         tenantB,
                         barrier))
        {
            var results = await Task.WhenAll(
                CreateProcessor(contextA, tenantA).ExecuteAsync(
                    1,
                    "mysql-worker",
                    CancellationToken.None),
                CreateProcessor(contextB, tenantB).ExecuteAsync(
                    1,
                    "mysql-worker",
                    CancellationToken.None));

            Assert.Single(
                results,
                result => result.Outcome
                    == AIAnalysisJobProcessingOutcome.FallbackCompleted);
            Assert.Single(
                results,
                result => result.Outcome
                    is AIAnalysisJobProcessingOutcome.LostRace
                        or AIAnalysisJobProcessingOutcome.AlreadyTerminal);
        }

        var persisted = await ReloadAsync(database.ConnectionString, centerId);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, persisted.Attempt.Status);
        Assert.Equal(2ul, persisted.Attempt.RowVersion);
        Assert.Equal(AIJobStatus.FallbackCompleted, persisted.Job.Status);
        Assert.Equal(2ul, persisted.Job.RowVersion);
        Assert.Single(persisted.Analyses);
    }

    [MySqlIntegrationFact]
    public async Task ReasoningAnalysis_RelationalUniqueConstraint_RejectsDuplicateAttempt()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, centerId);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using (var processorContext = CreateContext(
                         database.ConnectionString,
                         tenant))
        {
            var result = await CreateProcessor(processorContext, tenant)
                .ExecuteAsync(1, "mysql-worker", CancellationToken.None);
            Assert.Equal(
                AIAnalysisJobProcessingOutcome.FallbackCompleted,
                result.Outcome);
        }

        await using (var duplicateContext = CreateContext(
                         database.ConnectionString,
                         tenant))
        {
            duplicateContext.ReasoningAnalyses.Add(
                new RuleBasedFallbackBuilder().Build(new RuleBasedFallbackInput(
                    centerId,
                    1,
                    true,
                    1,
                    false,
                    "en",
                    UtcNow.AddSeconds(1))));

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                duplicateContext.SaveChangesAsync());
        }

        var persisted = await ReloadAsync(database.ConnectionString, centerId);
        Assert.Single(persisted.Analyses);
    }

    private static AIAnalysisJobProcessor CreateProcessor(
        EduTwinDbContext context,
        TenantContext tenant) =>
        new(
            context,
            tenant,
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
            new FixedTimeProvider(UtcNow));

    private static EduTwinDbContext CreateContext(
        string connectionString,
        TenantContext tenant,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new EduTwinDbContext(options.Options, tenant);
    }

    private static async Task SeedAsync(
        string connectionString,
        Guid centerId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(connectionString, tenant);
        await context.Database.OpenConnectionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");
            context.Attempts.Add(new Attempt
            {
                AttemptId = 1,
                CenterId = centerId,
                StudentId = Guid.NewGuid(),
                QuestionId = 1,
                FinalAnswer = "relational-test-answer",
                ReasoningText = "relational-test-reasoning",
                IsCorrect = true,
                AwardedScore = 1,
                TimeSpentSeconds = 20,
                Confidence = 80,
                AnswerChanges = 0,
                Skipped = false,
                ReasoningLanguage = "en",
                Status = AttemptStatus.PendingAnalysis,
                ClientSubmissionId = Guid.NewGuid(),
                CreatedAt = UtcNow.AddMinutes(-2),
                CreatedBy = Guid.NewGuid(),
                UpdatedAt = UtcNow.AddMinutes(-2)
            });
            context.AIAnalysisJobs.Add(new AIAnalysisJob
            {
                AnalysisJobId = 1,
                CenterId = centerId,
                AttemptId = 1,
                Status = AIJobStatus.Processing,
                RetryCount = 0,
                AvailableAt = UtcNow.AddMinutes(-2),
                StartedAt = UtcNow.AddMinutes(-1),
                LeaseOwner = "mysql-worker",
                LeaseUntil = UtcNow.AddMinutes(5),
                CorrelationId = "mysql-processor-test",
                CreatedAt = UtcNow.AddMinutes(-2),
                UpdatedAt = UtcNow.AddMinutes(-1)
            });
            await context.SaveChangesAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<PersistedState> ReloadAsync(
        string connectionString,
        Guid centerId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(connectionString, tenant);
        return new PersistedState(
            await context.Attempts.AsNoTracking().SingleAsync(),
            await context.AIAnalysisJobs.AsNoTracking().SingleAsync(),
            await context.ReasoningAnalyses.AsNoTracking().ToArrayAsync());
    }

    private sealed record PersistedState(
        Attempt Attempt,
        AIAnalysisJob Job,
        IReadOnlyList<ReasoningAnalysis> Analyses);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class ThrowAfterSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new InvalidOperationException(
                "Deliberate failure after relational commands were executed."));
    }

    private sealed class CompetingSaveBarrier(int participantCount) :
        SaveChangesInterceptor
    {
        private int _remaining = participantCount;
        private readonly TaskCompletionSource _allArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref _remaining) == 0)
            {
                _allArrived.TrySetResult();
            }

            await _allArrived.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(
            string adminConnectionString,
            string databaseName,
            string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<MySqlTestDatabase> CreateAsync()
        {
            var configuredConnection = Environment.GetEnvironmentVariable(
                AdminConnectionVariable)
                ?? throw new InvalidOperationException(
                    $"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_p11t05_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(
                adminBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            var database = new MySqlTestDatabase(
                adminBuilder.ConnectionString,
                databaseName,
                databaseBuilder.ConnectionString);
            try
            {
                var tenant = new TenantContext();
                await using var context = CreateContext(database.ConnectionString, tenant);
                await context.Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new MySqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
            await command.ExecuteNonQueryAsync();
        }
    }
}
