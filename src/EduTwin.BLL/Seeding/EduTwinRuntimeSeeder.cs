using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Seeding;
using EduTwin.BLL.KnowledgeGraph;

namespace EduTwin.BLL.Seeding;

public class EduTwinRuntimeSeeder
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ILogger<EduTwinRuntimeSeeder> _logger;
    private readonly IConfiguration _config;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly KnowledgeGraphValidator _dagValidator;
    private readonly IManifestEvaluator _evaluator;

    public EduTwinRuntimeSeeder(
        EduTwinDbContext dbContext,
        ILogger<EduTwinRuntimeSeeder> logger,
        IConfiguration config,
        IPasswordHasher<User> passwordHasher,
        IManifestEvaluator evaluator)
    {
        _dbContext = dbContext;
        _logger = logger;
        _config = config;
        _passwordHasher = passwordHasher;
        _dagValidator = new KnowledgeGraphValidator();
        _evaluator = evaluator;
    }

    public async Task SeedAsync()
    {
        var isDevelopment = RuntimeEnvironmentSecurity.IsDevelopment(_config);
        var centerManagerPassword = _config["Seed:CenterManagerPassword"];
        RuntimeEnvironmentSecurity.EnsureSecureCredential(centerManagerPassword, "Seed:CenterManagerPassword", isDevelopment);

        var teacherPassword = _config["Seed:TeacherPassword"];
        var studentPassword = _config["Seed:StudentPassword"];

        if (!string.IsNullOrWhiteSpace(teacherPassword))
        {
            RuntimeEnvironmentSecurity.EnsureSecureCredential(teacherPassword, "Seed:TeacherPassword", isDevelopment);
        }

        if (!string.IsNullOrWhiteSpace(studentPassword))
        {
            RuntimeEnvironmentSecurity.EnsureSecureCredential(studentPassword, "Seed:StudentPassword", isDevelopment);
        }

        var effectiveTeacherPassword = string.IsNullOrWhiteSpace(teacherPassword) ? centerManagerPassword! : teacherPassword;
        var effectiveStudentPassword = string.IsNullOrWhiteSpace(studentPassword) ? centerManagerPassword! : studentPassword;

        if (!isDevelopment && RuntimeEnvironmentSecurity.IsStaging(_config))
        {
            if (string.IsNullOrWhiteSpace(teacherPassword) || string.IsNullOrWhiteSpace(studentPassword))
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY ERROR: In Staging, Seed:TeacherPassword and Seed:StudentPassword must be explicitly configured instead of sharing Seed:CenterManagerPassword across all roles.");
            }

            if (string.Equals(centerManagerPassword, teacherPassword, StringComparison.Ordinal) ||
                string.Equals(centerManagerPassword, studentPassword, StringComparison.Ordinal) ||
                string.Equals(teacherPassword, studentPassword, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "CRITICAL SECURITY ERROR: In Staging, seeded CenterManager, Teacher, and Student roles must use distinct passwords.");
            }
        }

        var statusA = await _evaluator.EvaluateTenantAsync(true);
        var statusB = await _evaluator.EvaluateTenantAsync(false);

        var plan = SeedExecutionPlan.Create(statusA, statusB);

        if (plan.HasConflict)
        {
            _logger.LogWarning("Tenant conflict detected. A={StatusA}, B={StatusB}. Skipping seeding for conflicting tenants so the app can start.", statusA, statusB);
            return;
        }

        if (plan.IsNoOp)
        {
            _logger.LogInformation("Database is already fully seeded according to manifest. Skipping seed.");
            return;
        }

        var factoryA = new EduTwinSeedFactory(true);
        var dataA = plan.ShouldSeedA ? factoryA.CreateData() : null;

        var factoryB = new EduTwinSeedFactory(false);
        var dataB = plan.ShouldSeedB ? factoryB.CreateData() : null;

        // R14: Cross-tenant isolation assertions (if both are generated)
        if (dataA != null && dataB != null)
        {
            ValidateTenantIsolation(dataA, dataB);
        }

        // Apply password hashes (R08 & R18)
        var applicator = new SeedPasswordApplicator(_passwordHasher);
        applicator.ApplyHashes(plan, dataA, dataB, centerManagerPassword!, effectiveTeacherPassword, effectiveStudentPassword);

        // Validate DAGs (R10)
        if (dataA != null) _dagValidator.ValidateDag(dataA.Edges);
        if (dataB != null) _dagValidator.ValidateDag(dataB.Edges);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            if (dataA != null) await InsertDataAsync(dataA);
            if (dataB != null) await InsertDataAsync(dataB);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Seeding applied successfully. Tenant A: {StatusA}, Tenant B: {StatusB}, Hashes: {Hashes}", statusA, statusB, plan.ExpectedUsersToHash);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error seeding database. Transaction rolled back completely.");
            throw;
        }
    }

    private void ValidateTenantIsolation(SeedDataContainer dataA, SeedDataContainer dataB)
    {
        if (dataA.Users.Any(ua => dataB.Users.Any(ub => ub.UserId == ua.UserId)))
            throw new InvalidOperationException("Cross-tenant leak detected: Shared User ID.");

        if (dataA.Students.Any(sa => dataB.ClassStudents.Any(csb => csb.StudentId == sa.StudentId)))
            throw new InvalidOperationException("Cross-tenant leak detected: Student A in Class B.");

        if (dataA.Topics.Any(ta => dataB.Edges.Any(eb => eb.SourceNodeId == ta.NodeId || eb.TargetNodeId == ta.NodeId)))
            throw new InvalidOperationException("Cross-tenant leak detected: Topic A used in Edge B.");
    }

    private async Task InsertDataAsync(SeedDataContainer data)
    {
        if (data.SkippedQuestionTemplates.Count > 0)
            _logger.LogWarning("Seed templates excluded because grade/topic scope needs review: {TemplateCodes}", string.Join(", ", data.SkippedQuestionTemplates));
        _dbContext.Centers.Add(data.Center);
        _dbContext.Users.AddRange(data.Users);
        _dbContext.Teachers.AddRange(data.Teachers);
        _dbContext.Students.AddRange(data.Students);
        _dbContext.Subjects.AddRange(data.Subjects);
        _dbContext.Classes.AddRange(data.Classes);
        _dbContext.ClassStudents.AddRange(data.ClassStudents);

        _dbContext.KnowledgeNodes.AddRange(data.Topics);
        _dbContext.KnowledgeEdges.AddRange(data.Edges);

        var currentVersionIds = data.GraphVersions.Where(v => v.IsCurrent).Select(v => v.GraphVersionId).ToHashSet();
        foreach (var version in data.GraphVersions) { version.Status = "Draft"; version.IsCurrent = false; }
        _dbContext.KnowledgeGraphVersions.AddRange(data.GraphVersions);
        await _dbContext.SaveChangesAsync();
        // Publish prerequisites before dependent grade versions. Frozen topology is never edited after publication.
        foreach (var version in data.GraphVersions.OrderBy(v => v.GradeLevel).ThenBy(v => v.GraphVersionId))
        {
            _dbContext.KnowledgeNodeGrades.AddRange(data.NodeGrades.Where(m => m.GraphVersionId == version.GraphVersionId));
            // Endpoint membership is a trigger dependency, not an EF FK to the mapping table.
            // Persist it before edges so batching cannot insert a relation before its mapped nodes.
            await _dbContext.SaveChangesAsync();
            _dbContext.KnowledgeEdgeGrades.AddRange(data.EdgeGrades.Where(m => m.GraphVersionId == version.GraphVersionId));
            await _dbContext.SaveChangesAsync();
            version.Status = "Published"; version.IsCurrent = currentVersionIds.Contains(version.GraphVersionId);
            await _dbContext.SaveChangesAsync();
        }

        foreach (var curriculum in data.Curriculums) curriculum.ReviewStatus = EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Draft;
        _dbContext.Curriculums.AddRange(data.Curriculums);
        _dbContext.CurriculumClasses.AddRange(data.CurriculumClasses);
        _dbContext.CurriculumNodes.AddRange(data.CurriculumNodes);
        await _dbContext.SaveChangesAsync();
        foreach (var curriculum in data.Curriculums) curriculum.ReviewStatus = EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Published;
        await _dbContext.SaveChangesAsync();
        _dbContext.ClassCurriculumApplications.AddRange(data.CurriculumApplications);

        _dbContext.Questions.AddRange(data.Questions);
        _dbContext.QuestionOptions.AddRange(data.QuestionOptions);
        _dbContext.QuestionKnowledgeNodes.AddRange(data.QuestionNodes);

        _dbContext.StudentSubjectGoals.AddRange(data.Goals);
    }
}

public class SeedPasswordApplicator
{
    private readonly IPasswordHasher<User> _passwordHasher;
    public SeedPasswordApplicator(IPasswordHasher<User> passwordHasher) => _passwordHasher = passwordHasher;

    public void ApplyHashes(
        SeedExecutionPlan plan,
        SeedDataContainer? dataA,
        SeedDataContainer? dataB,
        string centerManagerPassword,
        string? teacherPassword = null,
        string? studentPassword = null)
    {
        if (plan.HasConflict || plan.IsNoOp) return;
        var resolvedTeacher = string.IsNullOrWhiteSpace(teacherPassword) ? centerManagerPassword : teacherPassword;
        var resolvedStudent = string.IsNullOrWhiteSpace(studentPassword) ? centerManagerPassword : studentPassword;
        if (plan.ShouldSeedA && dataA != null) ApplyPasswordHashes(dataA, centerManagerPassword, resolvedTeacher, resolvedStudent);
        if (plan.ShouldSeedB && dataB != null) ApplyPasswordHashes(dataB, centerManagerPassword, resolvedTeacher, resolvedStudent);
    }

    private void ApplyPasswordHashes(SeedDataContainer data, string centerManagerPassword, string teacherPassword, string studentPassword)
    {
        foreach (var user in data.Users)
        {
            var rolePassword = user.RoleName switch
            {
                EduTwin.Contracts.IdentityAndTenancy.UserRole.Teacher => teacherPassword,
                EduTwin.Contracts.IdentityAndTenancy.UserRole.Student => studentPassword,
                _ => centerManagerPassword
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, rolePassword);
        }
    }
}
