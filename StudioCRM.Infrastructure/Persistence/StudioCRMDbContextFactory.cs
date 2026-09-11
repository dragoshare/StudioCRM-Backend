using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudioCRM.Infrastructure.Persistence;

public class StudioCRMDbContextFactory : IDesignTimeDbContextFactory<StudioCRMDbContext>
{
    public StudioCRMDbContext CreateDbContext(string[] args)
    {
        var connectionName = Environment.GetEnvironmentVariable("Database__ConnectionName")
            ?? "TestConnection";
        var connectionString = Environment.GetEnvironmentVariable($"ConnectionStrings__{connectionName}");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set ConnectionStrings__{connectionName} before running Entity Framework tools.");
        }

        var options = new DbContextOptionsBuilder<StudioCRMDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new StudioCRMDbContext(options);
    }
}
