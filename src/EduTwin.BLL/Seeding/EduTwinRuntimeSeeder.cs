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
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly KnowledgeGraphValidator _dagValidator;

    public EduTwinRuntimeSeeder(
        EduTwinDbContext dbContext,
        ILogger<EduTwinRuntimeSeeder> logger,
        IConfiguration config)
    {
        _dbContext = dbContext;
        _logger = logger;
        _config = config;
        _passwordHasher = new PasswordHasher<User>();
        _dagValidator = new KnowledgeGraphValidator();
    }

    public async Task SeedAsync()
    {
        var password = _config["Seed:CenterManagerPassword"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException("Secure Seed:CenterManagerPassword is required and must be at least 12 characters.");
        }

        await SeedCenterAsync(true, password);
        _dbContext.ChangeTracker.Clear();
        await SeedCenterAsync(false, password);
    }

    private async Task SeedCenterAsync(bool isCenterA, string password)
    {
        var factory = new EduTwinSeedFactory(isCenterA);
        
        // Use a dummy user to hash the password for this specific run
        var passwordHash = _passwordHasher.HashPassword(new User(), password);
        var data = factory.CreateData(passwordHash);

        var centerId = data.Center.CenterId;
        var centerCode = data.Center.CenterCode;

        // Idempotency Check
        var existingCenter = await _dbContext.Centers.FirstOrDefaultAsync(c => c.CenterId == centerId);
        var existingCodeCenter = await _dbContext.Centers.FirstOrDefaultAsync(c => c.CenterCode == centerCode);

        if (existingCodeCenter != null && existingCodeCenter.CenterId != centerId)
        {
            throw new InvalidOperationException($"CenterCode {centerCode} exists with a different CenterId.");
        }

        if (existingCenter != null)
        {
            // Validate deterministic sentinel counts
            var userCount = await _dbContext.Users.CountAsync(u => u.CenterId == centerId);
            if (userCount < 8)
            {
                throw new InvalidOperationException($"Tenant {centerCode} is partially seeded or conflicting.");
            }
            
            _logger.LogInformation("Tenant {CenterCode} already exists. Skipping seed.", centerCode);
            return;
        }

        // Validate DAG before saving
        _dagValidator.ValidateDag(data.Edges);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
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

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Tenant {CenterCode} seeded successfully.", centerCode);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error seeding Tenant {CenterCode}.", centerCode);
            throw;
        }
    }
}
