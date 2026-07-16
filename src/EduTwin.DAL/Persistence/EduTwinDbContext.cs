using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace EduTwin.DAL.Persistence;

public class EduTwinDbContext : DbContext
{
    public EduTwinDbContext(DbContextOptions<EduTwinDbContext> options) : base(options)
    {
    }

    public DbSet<Center> Centers => Set<Center>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassStudent> ClassStudents => Set<ClassStudent>();

    // KnowledgeGraph
    public DbSet<KnowledgeNode> KnowledgeNodes => Set<KnowledgeNode>();
    public DbSet<KnowledgeEdge> KnowledgeEdges => Set<KnowledgeEdge>();

    // CurriculumAndQuestions
    public DbSet<Curriculum> Curriculums => Set<Curriculum>();
    public DbSet<CurriculumClass> CurriculumClasses => Set<CurriculumClass>();
    public DbSet<CurriculumNode> CurriculumNodes => Set<CurriculumNode>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<QuestionKnowledgeNode> QuestionKnowledgeNodes => Set<QuestionKnowledgeNode>();

    // Assignments
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentQuestion> AssignmentQuestions => Set<AssignmentQuestion>();
    public DbSet<AssignmentTarget> AssignmentTargets => Set<AssignmentTarget>();
    public DbSet<StudentAssignmentProgress> StudentAssignmentProgresses => Set<StudentAssignmentProgress>();

    // DigitalTwin
    public DbSet<StudentSubjectGoal> StudentSubjectGoals => Set<StudentSubjectGoal>();
    public DbSet<StudentTwin> StudentTwins => Set<StudentTwin>();
    public DbSet<KnowledgeTwin> KnowledgeTwins => Set<KnowledgeTwin>();
    public DbSet<BehaviorTwin> BehaviorTwins => Set<BehaviorTwin>();
    public DbSet<TwinUpdateHistory> TwinUpdateHistories => Set<TwinUpdateHistory>();

    // Recommendations
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<LearningPathItem> LearningPathItems => Set<LearningPathItem>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    // Assessment and Reasoning
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<ReasoningAnalysis> ReasoningAnalyses => Set<ReasoningAnalysis>();
    public DbSet<AIAnalysisJob> AIAnalysisJobs => Set<AIAnalysisJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all configurations defined in the current assembly (DAL)
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        
        configurationBuilder.Properties<Guid>().HaveConversion<Conventions.LowercaseGuidConverter>();
        configurationBuilder.Properties<Guid?>().HaveConversion<Conventions.LowercaseGuidConverter>();
        configurationBuilder.Properties<DateTime>().HaveConversion<Conventions.UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<Conventions.UtcDateTimeConverter>();
    }

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Models.IHasRowVersion>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.RowVersion = 1;
            }
            else if (entry.State == EntityState.Modified)
            {
                var original = entry.OriginalValues.GetValue<ulong>(nameof(Models.IHasRowVersion.RowVersion));
                checked
                {
                    entry.Entity.RowVersion = original + 1;
                }
            }
        }
    }
}
