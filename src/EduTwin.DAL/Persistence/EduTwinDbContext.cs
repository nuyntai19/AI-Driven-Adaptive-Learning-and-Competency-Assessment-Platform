using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
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
