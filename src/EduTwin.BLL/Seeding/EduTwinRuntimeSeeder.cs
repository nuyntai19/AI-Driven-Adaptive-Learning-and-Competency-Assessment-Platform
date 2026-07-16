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
        var password = _config["Seed:CenterManagerPassword"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException("Secure Seed:CenterManagerPassword is required and must be at least 12 characters.");
        }

        var statusA = await _evaluator.EvaluateTenantAsync(true);
        var statusB = await _evaluator.EvaluateTenantAsync(false);

        var plan = SeedExecutionPlan.Create(statusA, statusB);

        if (plan.HasConflict)
        {
            _logger.LogError("Tenant conflict detected. A={StatusA}, B={StatusB}. Cannot proceed with seeding.", statusA, statusB);
            throw new InvalidOperationException($"Tenant conflict detected. A={statusA}, B={statusB}. Cannot proceed with seeding.");
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
        applicator.ApplyHashes(plan, dataA, dataB, password);

        // Validate DAGs (R10)
        if (dataA != null) _dagValidator.ValidateDag(dataA.Edges);
        if (dataB != null) _dagValidator.ValidateDag(dataB.Edges);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            if (dataA != null) InsertData(dataA);
            if (dataB != null) InsertData(dataB);

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

    private void InsertData(SeedDataContainer data)
    {
        _dbContext.Centers.Add(data.Center);
        _dbContext.Users.AddRange(data.Users);
        _dbContext.Teachers.AddRange(data.Teachers);
        _dbContext.Students.AddRange(data.Students);
        _dbContext.Subjects.AddRange(data.Subjects);
        _dbContext.Classes.AddRange(data.Classes);
        _dbContext.ClassStudents.AddRange(data.ClassStudents);

        _dbContext.KnowledgeNodes.AddRange(data.Topics);
        _dbContext.KnowledgeEdges.AddRange(data.Edges);

        _dbContext.Curriculums.AddRange(data.Curriculums);
        _dbContext.CurriculumClasses.AddRange(data.CurriculumClasses);
        _dbContext.CurriculumNodes.AddRange(data.CurriculumNodes);

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

    public void ApplyHashes(SeedExecutionPlan plan, SeedDataContainer? dataA, SeedDataContainer? dataB, string password)
    {
        if (plan.HasConflict || plan.IsNoOp) return;
        if (plan.ShouldSeedA && dataA != null) ApplyPasswordHashes(dataA, password);
        if (plan.ShouldSeedB && dataB != null) ApplyPasswordHashes(dataB, password);
    }

    private void ApplyPasswordHashes(SeedDataContainer data, string password)
    {
        foreach (var user in data.Users)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }
    }
}
