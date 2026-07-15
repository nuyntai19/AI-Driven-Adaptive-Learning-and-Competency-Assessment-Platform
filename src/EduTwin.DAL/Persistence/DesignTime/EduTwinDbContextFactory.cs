using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EduTwin.DAL.Persistence.DesignTime;

public class EduTwinDbContextFactory : IDesignTimeDbContextFactory<EduTwinDbContext>
{
    public EduTwinDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'ConnectionStrings__Default' is not found in environment variables. Please set it before running EF tooling.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<EduTwinDbContext>();
        optionsBuilder.UseMySQL(connectionString);

        return new EduTwinDbContext(optionsBuilder.Options);
    }
}
