using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.DAL.Seeding;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

[Collection("MySqlDatabase")]
public sealed class QuestionEvaluationModeMySqlIntegrationTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime UtcNow = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

    [MySqlIntegrationFact]
    public async Task UpgradeMigration_FromGate3Baseline_BackfillsLegacyEssayToManual_AndPreservesMCQAndShortAnswer()
    {
        // 1. Create database and migrate ONLY up to the old Gate 3 migration
        const string oldGate3Migration = "20260912173142_AddEvaluationModeAndDisplayLatex";
        await using var database = await MySqlTestDatabase.CreateToMigrationAsync(oldGate3Migration);

        var tenant = new TenantContext();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        const ulong nodeId = 5001UL;
        const ulong essayQuestionId = 90001UL;
        const ulong mcqQuestionId = 90002UL;
        const ulong shortAnswerQuestionId = 90003UL;

        // 2. Insert hierarchy and legacy question data where Essay has answer_evaluation_mode = 'TextExact'
        await using (var seedContext = CreateContext(database.ConnectionString, tenant))
        {
            await seedContext.Database.OpenConnectionAsync();
            try
            {
                await seedContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                seedContext.Centers.Add(new Center
                {
                    CenterId = centerId,
                    CenterCode = $"C-{centerId:N}"[..10],
                    CenterName = "Upgrade Test Center",
                    Status = CenterStatus.Active,
                    Timezone = "UTC",
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                });

                seedContext.Users.Add(new User
                {
                    CenterId = centerId,
                    UserId = teacherId,
                    Username = $"teacher-{teacherId:N}",
                    PasswordHash = "hash",
                    DisplayName = "Teacher Upgrade",
                    RoleName = UserRole.Teacher,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                });

                seedContext.Teachers.Add(new Teacher
                {
                    CenterId = centerId,
                    TeacherId = teacherId,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                });

                seedContext.Subjects.Add(new Subject
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    SubjectCode = "UPG-MATH",
                    SubjectName = "Upgrade Math",
                    IsActive = true,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                });

                seedContext.KnowledgeNodes.Add(new KnowledgeNode
                {
                    CenterId = centerId,
                    NodeId = nodeId,
                    SubjectId = subjectId,
                    NodeType = NodeType.Topic,
                    NodeCode = "TOPIC-UPG",
                    NodeName = "Upgrade Topic",
                    OrderIndex = 1,
                    ExamImportance = 1.0m,
                    EstimatedLearningMinutes = 30,
                    IsActive = true,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                });

                await seedContext.SaveChangesAsync();

                // Raw insert to simulate legacy state before backfill migration existed
                const string emptyJson = "{}";
                await seedContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO questions (
                        center_id, question_id, subject_id, primary_topic_node_id, created_by_teacher_id,
                        question_type, difficulty, question_text, correct_answer, solution,
                        grading_criteria, max_score, estimated_time_seconds, language_code, status,
                        answer_evaluation_mode, created_at, updated_at, row_version
                    ) VALUES 
                    ({0}, {1}, {2}, {3}, {4}, 'Essay', 3, 'Legacy Essay Question', 'Legacy rubric', 'Legacy solution', {5}, 5.0, 300, 'vi', 'Active', 'TextExact', {6}, {6}, 1),
                    ({0}, {7}, {2}, {3}, {4}, 'MultipleChoice', 1, 'Legacy MCQ Question', 'A', 'Sol', {5}, 1.0, 60, 'vi', 'Active', 'TextExact', {6}, {6}, 1),
                    ({0}, {8}, {2}, {3}, {4}, 'ShortAnswer', 2, 'Legacy Short Answer Rational', '3/2', 'Sol', {5}, 2.0, 120, 'vi', 'Active', 'NumericRational', {6}, {6}, 1);",
                    centerId, essayQuestionId, subjectId, nodeId, teacherId, emptyJson, UtcNow, mcqQuestionId, shortAnswerQuestionId);
            }
            finally
            {
                await seedContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            }
        }

        // 3. Assert baseline state BEFORE upgrade
        await using (var verifyPreContext = CreateContext(database.ConnectionString, tenant))
        {
            var preEssayMode = await verifyPreContext.Database.SqlQueryRaw<string>(
                "SELECT answer_evaluation_mode AS Value FROM questions WHERE question_id = {0}", essayQuestionId).SingleAsync();
            Assert.Equal("TextExact", preEssayMode);
        }

        // 4. Perform database upgrade to latest (applies BackfillQuestionEvaluationModeMatrix migration)
        await using (var migrationContext = CreateContext(database.ConnectionString, tenant))
        {
            var migrator = migrationContext.Database.GetService<IMigrator>()
                ?? throw new InvalidOperationException("IMigrator service is not available.");
            await migrator.MigrateAsync();
        }

        // 5. Assert post-upgrade state on live MySQL:
        // - Legacy Essay question must now be 'Manual'
        // - MultipleChoice question remains 'TextExact'
        // - ShortAnswer question remains 'NumericRational'
        await using (var verifyPostContext = CreateContext(database.ConnectionString, tenant))
        {
            var postEssayMode = await verifyPostContext.Database.SqlQueryRaw<string>(
                "SELECT answer_evaluation_mode AS Value FROM questions WHERE question_id = {0}", essayQuestionId).SingleAsync();
            Assert.Equal("Manual", postEssayMode);

            var postMcqMode = await verifyPostContext.Database.SqlQueryRaw<string>(
                "SELECT answer_evaluation_mode AS Value FROM questions WHERE question_id = {0}", mcqQuestionId).SingleAsync();
            Assert.Equal("TextExact", postMcqMode);

            var postShortAnswerMode = await verifyPostContext.Database.SqlQueryRaw<string>(
                "SELECT answer_evaluation_mode AS Value FROM questions WHERE question_id = {0}", shortAnswerQuestionId).SingleAsync();
            Assert.Equal("NumericRational", postShortAnswerMode);

            // Verify strongly typed EF Core mapping with IgnoreQueryFilters
            var essayEntity = await verifyPostContext.Questions.IgnoreQueryFilters().SingleAsync(q => q.QuestionId == essayQuestionId);
            Assert.Equal(QuestionType.Essay, essayEntity.QuestionType);
            Assert.Equal(QuestionAnswerEvaluationMode.Manual, essayEntity.AnswerEvaluationMode);

            var mcqEntity = await verifyPostContext.Questions.IgnoreQueryFilters().SingleAsync(q => q.QuestionId == mcqQuestionId);
            Assert.Equal(QuestionType.MultipleChoice, mcqEntity.QuestionType);
            Assert.Equal(QuestionAnswerEvaluationMode.TextExact, mcqEntity.AnswerEvaluationMode);

            var saEntity = await verifyPostContext.Questions.IgnoreQueryFilters().SingleAsync(q => q.QuestionId == shortAnswerQuestionId);
            Assert.Equal(QuestionType.ShortAnswer, saEntity.QuestionType);
            Assert.Equal(QuestionAnswerEvaluationMode.NumericRational, saEntity.AnswerEvaluationMode);
        }
    }

    [MySqlIntegrationFact]
    public async Task FreshDatabase_WithSeedFactory_EnforcesEvaluationModeMatrixOnLiveMySql()
    {
        // 1. Create fresh database fully migrated to latest
        await using var database = await MySqlTestDatabase.CreateAsync();
        var tenant = new TenantContext();

        // 2. Populate full seed data from EduTwinSeedFactory
        var factory = new EduTwinSeedFactory(isCenterA: true);
        var seedData = factory.CreateData();

        await using (var seedContext = CreateContext(database.ConnectionString, tenant))
        {
            seedContext.Centers.Add(seedData.Center);
            seedContext.Users.AddRange(seedData.Users);
            seedContext.Teachers.AddRange(seedData.Teachers);
            seedContext.Students.AddRange(seedData.Students);
            seedContext.Subjects.AddRange(seedData.Subjects);
            seedContext.Classes.AddRange(seedData.Classes);
            seedContext.ClassStudents.AddRange(seedData.ClassStudents);
            seedContext.KnowledgeNodes.AddRange(seedData.Topics);
            seedContext.KnowledgeEdges.AddRange(seedData.Edges);
            seedContext.Curriculums.AddRange(seedData.Curriculums);
            seedContext.CurriculumClasses.AddRange(seedData.CurriculumClasses);
            seedContext.CurriculumNodes.AddRange(seedData.CurriculumNodes);
            seedContext.Questions.AddRange(seedData.Questions);
            seedContext.QuestionOptions.AddRange(seedData.QuestionOptions);

            await seedContext.SaveChangesAsync();
        }

        // 3. Query questions from live MySQL and assert exact matrix
        await using (var verifyContext = CreateContext(database.ConnectionString, tenant))
        {
            var allQuestions = await verifyContext.Questions.IgnoreQueryFilters().ToListAsync();
            Assert.NotEmpty(allQuestions);

            var mcqQuestions = allQuestions.Where(q => q.QuestionType == QuestionType.MultipleChoice).ToList();
            Assert.NotEmpty(mcqQuestions);
            foreach (var mcq in mcqQuestions)
            {
                Assert.Equal(QuestionAnswerEvaluationMode.TextExact, mcq.AnswerEvaluationMode);
            }

            var essayQuestions = allQuestions.Where(q => q.QuestionType == QuestionType.Essay).ToList();
            Assert.NotEmpty(essayQuestions);
            foreach (var essay in essayQuestions)
            {
                Assert.Equal(QuestionAnswerEvaluationMode.Manual, essay.AnswerEvaluationMode);
            }

            var shortAnswerQuestions = allQuestions.Where(q => q.QuestionType == QuestionType.ShortAnswer).ToList();
            Assert.NotEmpty(shortAnswerQuestions);
            foreach (var sa in shortAnswerQuestions)
            {
                Assert.True(
                    sa.AnswerEvaluationMode == QuestionAnswerEvaluationMode.NumericRational ||
                    sa.AnswerEvaluationMode == QuestionAnswerEvaluationMode.TextExact,
                    $"ShortAnswer question {sa.QuestionId} must have NumericRational or TextExact, got {sa.AnswerEvaluationMode}");
            }
        }

        // 4. Assert MySQL Check Constraint ck_questions_answer_evaluation_mode rejects invalid evaluation mode
        await using (var constraintContext = CreateContext(database.ConnectionString, tenant))
        {
            const string emptyJson = "{}";
            var ex = await Assert.ThrowsAsync<MySqlException>(async () =>
            {
                await constraintContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO questions (
                        center_id, question_id, subject_id, primary_topic_node_id, created_by_teacher_id,
                        question_type, difficulty, question_text, correct_answer, solution,
                        grading_criteria, max_score, estimated_time_seconds, language_code, status,
                        answer_evaluation_mode, created_at, updated_at, row_version
                    ) VALUES (
                        {0}, 99999, {1}, {2}, {3},
                        'ShortAnswer', 1, 'Bad Mode Question', 'Ans', 'Sol',
                        {4}, 1.0, 60, 'vi', 'Active',
                        'InvalidBogusMode', {5}, {5}, 1
                    );",
                    seedData.Center.CenterId, seedData.Subjects[0].SubjectId, seedData.Topics[0].NodeId, seedData.Teachers[0].TeacherId, emptyJson, UtcNow);
            });

            Assert.Contains("ck_questions_answer_evaluation_mode", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static EduTwinDbContext CreateContext(string connectionString, TenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString)
            .Options;
        return new EduTwinDbContext(options, tenant);
    }

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static Task<MySqlTestDatabase> CreateAsync() => CreateToMigrationAsync(null);

        public static async Task<MySqlTestDatabase> CreateToMigrationAsync(string? targetMigration)
        {
            var configuredConnection = Environment.GetEnvironmentVariable(AdminConnectionVariable)
                ?? throw new InvalidOperationException($"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_gate3_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(adminBuilder.ConnectionString)
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
                var migrator = context.Database.GetService<IMigrator>()
                    ?? throw new InvalidOperationException("IMigrator service is not available.");

                if (string.IsNullOrWhiteSpace(targetMigration))
                {
                    await migrator.MigrateAsync();
                }
                else
                {
                    await migrator.MigrateAsync(targetMigration);
                }

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
            try
            {
                await using var connection = new MySqlConnection(_adminConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
