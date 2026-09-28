using Microsoft.EntityFrameworkCore;
using StudioCRM.Infrastructure.Persistence;
namespace StudioCRM.Tests.UnitTests;
public class ClientMigrationTests
{
    [Fact]
    public void MigrationSnapshotMatchesCurrentModel()
    {
        using var db = new StudioCRMDbContext(new DbContextOptionsBuilder<StudioCRMDbContext>().UseNpgsql("Host=localhost;Database=model_only;Username=model_only").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
