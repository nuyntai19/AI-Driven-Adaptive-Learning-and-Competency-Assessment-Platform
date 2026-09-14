using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Moq;
using MySql.Data.MySqlClient;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

[Collection("MySqlDatabase")]
public sealed class CenterManagerLiveMySqlTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime FixedUtcNow = new(2026, 9, 14, 14, 0, 0, DateTimeKind.Utc);
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly PasswordHasher<User> _passwordHasher;

    public CenterManagerLiveMySqlTests()
    {
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider
            .Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));
        _passwordHasher = new PasswordHasher<User>();
    }

    [MySqlIntegrationFact]
    public async Task Migration_FreshDatabase_AppliesLatestAndBootstrapsPermissions()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var tenant = new TenantContext();
        await using (var seedContext = CreateContext(database.ConnectionString, tenant))
        {
            await SeedCenterWithManagerAsync(seedContext, centerId, managerId);
        }

        await using (var bootContext = CreateContext(database.ConnectionString, tenant))
        {
            var bootstrapper = new AuthorizationBootstrapper(bootContext, _mockTimeProvider.Object);
            await bootstrapper.EnsureAsync();
        }

        await using (var verifyContext = CreateContext(database.ConnectionString, tenant))
        {
            var grantedCodes = await (
                from rp in verifyContext.RolePermissions.IgnoreQueryFilters()
                join r in verifyContext.AuthorizationRoles.IgnoreQueryFilters() on rp.RoleId equals r.RoleId
                join p in verifyContext.Permissions.IgnoreQueryFilters() on rp.PermissionId equals p.PermissionId
                where rp.CenterId == centerId && r.RoleCode == "SYSTEM_CENTERMANAGER"
                select p.PermissionCode
            ).ToListAsync();

            Assert.Contains("organization.teachers.reset_password", grantedCodes);
            Assert.Contains("organization.students.reset_password", grantedCodes);
        }
    }

    [MySqlIntegrationFact]
    public async Task Migration_ExistingDbUpgrade_AddsResetPasswordPermissionsToSystemCenterManager()
    {
        await using var database = await MySqlTestDatabase.CreateToMigrationAsync("20260913165812_AddPlatformAccountManageOwnPermission");
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var tenant = new TenantContext();
        await using (var seedContext = CreateContext(database.ConnectionString, tenant))
        {
            await SeedCenterWithManagerAsync(seedContext, centerId, managerId);
        }

        // Bootstrap on pre-upgrade schema
        await using (var preUpgradeContext = CreateContext(database.ConnectionString, tenant))
        {
            var bootstrapper = new AuthorizationBootstrapper(preUpgradeContext, _mockTimeProvider.Object);
            await bootstrapper.EnsureAsync();
        }

        // Verify pre-upgrade: reset-password permissions do not exist yet
        await using (var verifyPreContext = CreateContext(database.ConnectionString, tenant))
        {
            var preGranted = await (
                from rp in verifyPreContext.RolePermissions.IgnoreQueryFilters()
                join r in verifyPreContext.AuthorizationRoles.IgnoreQueryFilters() on rp.RoleId equals r.RoleId
                join p in verifyPreContext.Permissions.IgnoreQueryFilters() on rp.PermissionId equals p.PermissionId
                where rp.CenterId == centerId && r.RoleCode == "SYSTEM_CENTERMANAGER" &&
                      (p.PermissionCode == "organization.teachers.reset_password" || p.PermissionCode == "organization.students.reset_password")
                select p.PermissionCode
            ).ToListAsync();

            Assert.Empty(preGranted);
        }

        // Upgrade schema to latest migration (20260914133545)
        await using (var migrateContext = CreateContext(database.ConnectionString, tenant))
        {
            var migrator = migrateContext.Database.GetService<IMigrator>()
                ?? throw new InvalidOperationException("IMigrator service not available.");
            await migrator.MigrateAsync();
        }

        // Run runtime authorization bootstrap after migration upgrade
        await using (var postUpgradeContext = CreateContext(database.ConnectionString, tenant))
        {
            var bootstrapper = new AuthorizationBootstrapper(postUpgradeContext, _mockTimeProvider.Object);
            await bootstrapper.EnsureAsync();
        }

        // Verify post-upgrade: both permissions now granted to SYSTEM_CENTERMANAGER
        await using (var verifyPostContext = CreateContext(database.ConnectionString, tenant))
        {
            var postGranted = await (
                from rp in verifyPostContext.RolePermissions.IgnoreQueryFilters()
                join r in verifyPostContext.AuthorizationRoles.IgnoreQueryFilters() on rp.RoleId equals r.RoleId
                join p in verifyPostContext.Permissions.IgnoreQueryFilters() on rp.PermissionId equals p.PermissionId
                where rp.CenterId == centerId && r.RoleCode == "SYSTEM_CENTERMANAGER"
                select p.PermissionCode
            ).ToListAsync();

            Assert.Contains("organization.teachers.reset_password", postGranted);
            Assert.Contains("organization.students.reset_password", postGranted);
        }
    }

    [MySqlIntegrationFact]
    public async Task ResetAccountPassword_OnLiveMySql_EnforcesOcc_BumpsAuthVersion_RevokesTokens_RecordsAuditWithCorrectRowVersion()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var tenant = new TenantContext();
        await using (var seedContext = CreateContext(database.ConnectionString, tenant))
        {
            await SeedCenterWithManagerAsync(seedContext, centerId, managerId);

            var teacherUser = new User
            {
                UserId = teacherId,
                CenterId = centerId,
                Username = "teacher.live.test",
                DisplayName = "Teacher Live Test",
                PasswordHash = _passwordHasher.HashPassword(null!, "InitialPassword123!"),
                RoleName = UserRole.Teacher,
                Status = UserStatus.Active,
                AuthVersion = 1,
                RowVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            seedContext.Users.Add(teacherUser);

            var teacher = new Teacher
            {
                TeacherId = teacherId,
                CenterId = centerId,
                Department = "Khoa Toan",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            seedContext.Teachers.Add(teacher);

            // Active refresh tokens
            seedContext.RefreshTokens.AddRange(
                new RefreshToken
                {
                    CenterId = centerId,
                    UserId = teacherId,
                    TokenHash = "tokenhash_1",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    CreatedAt = FixedUtcNow
                },
                new RefreshToken
                {
                    CenterId = centerId,
                    UserId = teacherId,
                    TokenHash = "tokenhash_2",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    CreatedAt = FixedUtcNow
                }
            );

            await seedContext.SaveChangesAsync();
        }

        // Test OCC rejection with stale ExpectedUserRowVersion
        var cmTenant = new TenantContext();
        cmTenant.Initialize(centerId, managerId, nameof(UserRole.CenterManager), 1);

        await using (var staleContext = CreateContext(database.ConnectionString, cmTenant))
        {
            var staleUseCase = new ResetAccountPasswordUseCase(
                staleContext,
                cmTenant,
                _passwordHasher,
                _mockTimeProvider.Object,
                Mock.Of<ILogger<ResetAccountPasswordUseCase>>());

            var staleResult = await staleUseCase.ExecuteAsync(
                teacherId,
                nameof(UserRole.Teacher),
                new ResetAccountPasswordRequest
                {
                    NewPassword = "NewStrongPassword123!",
                    ExpectedUserRowVersion = "999", // Stale!
                    Reason = "Password reset attempt with stale version."
                });

            Assert.False(staleResult.IsSuccess);
            Assert.Equal(ErrorCodes.ConcurrencyConflict, staleResult.ErrorCode);
        }

        // Test successful reset with valid ExpectedUserRowVersion
        ResetAccountPasswordResult successResult;
        await using (var validContext = CreateContext(database.ConnectionString, cmTenant))
        {
            var validUseCase = new ResetAccountPasswordUseCase(
                validContext,
                cmTenant,
                _passwordHasher,
                _mockTimeProvider.Object,
                Mock.Of<ILogger<ResetAccountPasswordUseCase>>());

            successResult = await validUseCase.ExecuteAsync(
                teacherId,
                nameof(UserRole.Teacher),
                new ResetAccountPasswordRequest
                {
                    NewPassword = "BrandNewPassword123!",
                    ExpectedUserRowVersion = "1",
                    Reason = "Routine password reset requested by teacher."
                });

            Assert.True(successResult.IsSuccess);
            Assert.Equal(teacherId.ToString("D"), successResult.TargetUserId);
            Assert.Equal("2", successResult.NewRowVersion);
        }

        // Verify mutations in MySQL
        await using (var verifyContext = CreateContext(database.ConnectionString, cmTenant))
        {
            var updatedUser = await verifyContext.Users
                .IgnoreQueryFilters()
                .SingleAsync(u => u.UserId == teacherId);

            Assert.Equal(2ul, updatedUser.RowVersion);
            Assert.Equal(2u, updatedUser.AuthVersion);
            var verification = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.PasswordHash, "BrandNewPassword123!");
            Assert.Equal(PasswordVerificationResult.Success, verification);

            // Verify refresh tokens are all revoked
            var tokens = await verifyContext.RefreshTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == teacherId)
                .ToListAsync();

            Assert.Equal(2, tokens.Count);
            Assert.All(tokens, t =>
            {
                Assert.NotNull(t.RevokedAt);
                Assert.Equal("Password reset by CenterManager.", t.RevokeReason);
            });

            // Verify audit log
            var audit = await verifyContext.AuthorizationAuditLogs
                .IgnoreQueryFilters()
                .SingleAsync(a => a.TargetUserId == teacherId && a.ActionType == "TeacherPasswordReset");

            Assert.Equal(centerId, audit.CenterId);
            Assert.Equal(managerId, audit.ActorUserId);
            Assert.Equal("Routine password reset requested by teacher.", audit.Reason);

            using var doc = JsonDocument.Parse(audit.AfterData ?? "{}");
            var auditRowVersion = doc.RootElement.GetProperty("RowVersion").GetUInt64();
            var auditAuthVersion = doc.RootElement.GetProperty("AuthVersion").GetUInt32();

            Assert.Equal(2ul, auditRowVersion);
            Assert.Equal(2u, auditAuthVersion);
            Assert.Equal(successResult.NewRowVersion, auditRowVersion.ToString(CultureInfo.InvariantCulture));
        }
    }

    [MySqlIntegrationFact]
    public async Task DeleteStudent_OnLiveMySql_SoftDeletes_RemovesMemberships_RevokesTokens_PreservesHistoricalEvidence()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        var tenant = new TenantContext();
        await using (var seedContext = CreateContext(database.ConnectionString, tenant))
        {
            await SeedCenterWithManagerAsync(seedContext, centerId, managerId);

            // Seed student user
            var studentUser = new User
            {
                UserId = studentId,
                CenterId = centerId,
                Username = "student.live.test",
                DisplayName = "Student Live Test",
                PasswordHash = "hash",
                RoleName = UserRole.Student,
                Status = UserStatus.Active,
                AuthVersion = 1,
                RowVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            seedContext.Users.Add(studentUser);

            var student = new Student
            {
                StudentId = studentId,
                CenterId = centerId,
                FullName = "Student Live Test",
                GradeLevel = 10,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            seedContext.Students.Add(student);

            // Wrap all seed data creation in disabled foreign key checks for clean isolated test setup
            await seedContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");
            try
            {
                // Seed subject
                seedContext.Subjects.Add(new Subject
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    SubjectCode = "TOAN10",
                    SubjectName = "Toán 10",
                    IsActive = true,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                });

                // Seed teacher user & teacher
                var teacherUser = new User
                {
                    UserId = teacherId,
                    CenterId = centerId,
                    Username = "teacher.del.test",
                    DisplayName = "Teacher Del Test",
                    PasswordHash = "hash",
                    RoleName = UserRole.Teacher,
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    RowVersion = 1,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                };
                seedContext.Users.Add(teacherUser);
                seedContext.Teachers.Add(new Teacher
                {
                    TeacherId = teacherId,
                    CenterId = centerId,
                    Department = "Toán",
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                });

                // Seed class and membership
                var classEntity = new Class
                {
                    ClassId = classId,
                    CenterId = centerId,
                    TeacherId = teacherId,
                    SubjectId = subjectId,
                    ClassName = "Lop Toan 10A",
                    AcademicYear = "2026-2027",
                    Status = ClassStatus.Active,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                };
                seedContext.Classes.Add(classEntity);

                seedContext.ClassStudents.Add(new ClassStudent
                {
                    CenterId = centerId,
                    ClassId = classId,
                    StudentId = studentId,
                    Status = ClassStudentStatus.Active,
                    JoinedAt = FixedUtcNow
                });

                // Seed refresh token
                seedContext.RefreshTokens.Add(new RefreshToken
                {
                    CenterId = centerId,
                    UserId = studentId,
                    TokenHash = "student_token_hash",
                    ExpiresAt = FixedUtcNow.AddDays(7),
                    CreatedAt = FixedUtcNow
                });

                // Seed knowledge node
                seedContext.KnowledgeNodes.Add(new KnowledgeNode
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    NodeId = 1,
                    NodeCode = "TOPIC-10-1",
                    NodeName = "Chu de 1",
                    NodeType = NodeType.Topic,
                    OrderIndex = 1,
                    ExamImportance = 80m,
                    EstimatedLearningMinutes = 60,
                    IsActive = true,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                });

                // Seed question
                seedContext.Questions.Add(new Question
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    PrimaryTopicNodeId = 1,
                    CreatedByTeacherId = teacherId,
                    QuestionId = 501,
                    QuestionText = "Cau hoi 1",
                    QuestionType = QuestionType.MultipleChoice,
                    CorrectAnswer = "B",
                    Solution = "Giai thich",
                    LanguageCode = "vi",
                    Difficulty = 2,
                    MaxScore = 100m,
                    EstimatedTimeSeconds = 60,
                    Status = QuestionStatus.Active,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                });

                seedContext.Attempts.Add(new Attempt
                {
                    CenterId = centerId,
                    StudentId = studentId,
                    QuestionId = 501,
                    AttemptId = 9001,
                    FinalAnswer = "B",
                    ReasoningLanguage = "vi",
                    Status = AttemptStatus.Completed,
                    AwardedScore = 95.5m,
                    Confidence = 0.9m,
                    ClientSubmissionId = Guid.NewGuid(),
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                });

                seedContext.StudentSubjectGoals.Add(new StudentSubjectGoal
                {
                    GoalId = 8001,
                    CenterId = centerId,
                    StudentId = studentId,
                    SubjectId = subjectId,
                    TargetScore = 9.0m,
                    RemainingDays = 30,
                    CurrentPredictedScore = 8.5m,
                    RiskScore = 0.1m,
                    CreatedAt = FixedUtcNow,
                    UpdatedAt = FixedUtcNow
                });

                await seedContext.SaveChangesAsync();
            }
            finally
            {
                await seedContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            }
        }

        // Execute DeleteStudentUseCase
        var cmTenant = new TenantContext();
        cmTenant.Initialize(centerId, managerId, nameof(UserRole.CenterManager), 1);

        await using (var deleteContext = CreateContext(database.ConnectionString, cmTenant))
        {
            var useCase = new DeleteStudentUseCase(
                deleteContext,
                cmTenant,
                _mockTimeProvider.Object,
                Mock.Of<ILogger<DeleteStudentUseCase>>());

            var result = await useCase.ExecuteAsync(studentId, traceId: "trace-del-student-live");
            Assert.True(result.IsSuccess);
        }

        // Verify in MySQL
        await using (var verifyContext = CreateContext(database.ConnectionString, cmTenant))
        {
            var student = await verifyContext.Students
                .IgnoreQueryFilters()
                .SingleAsync(s => s.StudentId == studentId);
            Assert.True(student.IsDeleted);
            Assert.NotNull(student.DeletedAt);
            Assert.Equal(managerId, student.DeletedBy);

            var user = await verifyContext.Users
                .IgnoreQueryFilters()
                .SingleAsync(u => u.UserId == studentId);
            Assert.True(user.IsDeleted);
            Assert.Equal(UserStatus.Disabled, user.Status);
            Assert.Equal(2u, user.AuthVersion);
            Assert.Equal(2ul, user.RowVersion);

            var membership = await verifyContext.ClassStudents
                .IgnoreQueryFilters()
                .SingleAsync(cs => cs.ClassId == classId && cs.StudentId == studentId);
            Assert.Equal(ClassStudentStatus.Removed, membership.Status);
            Assert.NotNull(membership.RemovedAt);

            var tokens = await verifyContext.RefreshTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == studentId)
                .ToListAsync();
            Assert.Single(tokens);
            Assert.NotNull(tokens[0].RevokedAt);

            // CRITICAL: Historical evidence is 100% PRESERVED
            var attempts = await verifyContext.Attempts
                .IgnoreQueryFilters()
                .Where(a => a.StudentId == studentId)
                .ToListAsync();
            Assert.Single(attempts);
            Assert.Equal(9001ul, attempts[0].AttemptId);
            Assert.Equal(95.5m, attempts[0].AwardedScore);
            Assert.Equal(AttemptStatus.Completed, attempts[0].Status);

            var goals = await verifyContext.StudentSubjectGoals
                .IgnoreQueryFilters()
                .Where(g => g.StudentId == studentId)
                .ToListAsync();
            Assert.Single(goals);
            Assert.Equal(9.0m, goals[0].TargetScore);

            // Audit log exists
            var audit = await verifyContext.AuthorizationAuditLogs
                .IgnoreQueryFilters()
                .SingleAsync(a => a.TargetUserId == studentId && a.ActionType == "StudentDeleted");
            Assert.Equal("trace-del-student-live", audit.TraceId);
        }
    }

    [MySqlIntegrationFact]
    public async Task ConcurrentResetAndPasswordUpdate_OnLiveMySql_EnforcesTransactionalOcc()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var tenant = new TenantContext();
        await using (var seedContext = CreateContext(database.ConnectionString, tenant))
        {
            await SeedCenterWithManagerAsync(seedContext, centerId, managerId);

            var teacherUser = new User
            {
                UserId = teacherId,
                CenterId = centerId,
                Username = "teacher.concurrency.test",
                DisplayName = "Teacher Concurrency Test",
                PasswordHash = _passwordHasher.HashPassword(null!, "InitialPassword123!"),
                RoleName = UserRole.Teacher,
                Status = UserStatus.Active,
                AuthVersion = 1,
                RowVersion = 1,
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            seedContext.Users.Add(teacherUser);

            var teacher = new Teacher
            {
                TeacherId = teacherId,
                CenterId = centerId,
                Department = "Khoa Ly",
                CreatedAt = FixedUtcNow,
                UpdatedAt = FixedUtcNow
            };
            seedContext.Teachers.Add(teacher);
            await seedContext.SaveChangesAsync();
        }

        // Prepare two separate scopes/contexts competing to mutate user at RowVersion = 1
        var tenant1 = new TenantContext();
        tenant1.Initialize(centerId, managerId, nameof(UserRole.CenterManager), 1);

        var tenant2 = new TenantContext();
        tenant2.Initialize(centerId, managerId, nameof(UserRole.CenterManager), 1);

        await using var context1 = CreateContext(database.ConnectionString, tenant1);
        await using var context2 = CreateContext(database.ConnectionString, tenant2);

        var useCase1 = new ResetAccountPasswordUseCase(
            context1,
            tenant1,
            _passwordHasher,
            _mockTimeProvider.Object,
            Mock.Of<ILogger<ResetAccountPasswordUseCase>>());

        var useCase2 = new ResetAccountPasswordUseCase(
            context2,
            tenant2,
            _passwordHasher,
            _mockTimeProvider.Object,
            Mock.Of<ILogger<ResetAccountPasswordUseCase>>());

        // Launch concurrently
        var task1 = useCase1.ExecuteAsync(
            teacherId,
            nameof(UserRole.Teacher),
            new ResetAccountPasswordRequest
            {
                NewPassword = "ConcurrentPasswordOne123!",
                ExpectedUserRowVersion = "1",
                Reason = "Concurrent reset attempt 1."
            });

        var task2 = useCase2.ExecuteAsync(
            teacherId,
            nameof(UserRole.Teacher),
            new ResetAccountPasswordRequest
            {
                NewPassword = "ConcurrentPasswordTwo123!",
                ExpectedUserRowVersion = "1",
                Reason = "Concurrent reset attempt 2."
            });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);
        var conflictCount = results.Count(r => !r.IsSuccess && r.ErrorCode == ErrorCodes.ConcurrencyConflict);

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        // Verify database consistency
        await using (var verifyContext = CreateContext(database.ConnectionString, tenant1))
        {
            var teacherUser = await verifyContext.Users
                .IgnoreQueryFilters()
                .SingleAsync(u => u.UserId == teacherId);

            Assert.Equal(2ul, teacherUser.RowVersion);
            Assert.Equal(2u, teacherUser.AuthVersion);

            var audits = await verifyContext.AuthorizationAuditLogs
                .IgnoreQueryFilters()
                .Where(a => a.TargetUserId == teacherId && a.ActionType == "TeacherPasswordReset")
                .ToListAsync();

            Assert.Single(audits);
        }
    }

    private static async Task SeedCenterWithManagerAsync(EduTwinDbContext context, Guid centerId, Guid managerId)
    {
        var centerCode = $"C_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var username = $"cm_{Guid.NewGuid():N}"[..12];

        // 1. Insert Center first with primary_manager_user_id = NULL
        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO centers (center_id, center_code, center_name, status, primary_manager_user_id, timezone, created_at, updated_at, row_version)
              VALUES ({0}, {1}, 'Test Center', 'Active', NULL, 'Asia/Ho_Chi_Minh', {2}, {2}, 1);",
            centerId, centerCode, FixedUtcNow);

        // 2. Insert User (manager) referencing centerId
        await context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO users (user_id, center_id, username, display_name, password_hash, role_name, status, auth_version, created_at, updated_at, row_version)
              VALUES ({0}, {1}, {2}, 'Center Manager', 'hash', 'CenterManager', 'Active', 1, {3}, {3}, 1);",
            managerId, centerId, username, FixedUtcNow);

        // 3. Update Center with primary_manager_user_id = managerId
        await context.Database.ExecuteSqlRawAsync(
            @"UPDATE centers SET primary_manager_user_id = {0} WHERE center_id = {1};",
            managerId, centerId);
    }

    private static EduTwinDbContext CreateContext(
        string connectionString,
        TenantContext tenant,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        if (interceptors != null && interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }
        return new EduTwinDbContext(options.Options, tenant);
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
            var databaseName = $"edutwin_cm_{Guid.NewGuid():N}";
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
