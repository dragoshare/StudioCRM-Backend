using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudioCRM.Infrastructure.Persistence;

namespace StudioCRM.Tests.IntegrationTests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string schema = "crm_test_" + Guid.NewGuid().ToString("N");
    private readonly string connectionString;
    private TestDatabase(string connectionString) => this.connectionString = connectionString;
    public StudioCRMDbContext Context()
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false, IncludeErrorDetail = false };
        return new(new DbContextOptionsBuilder<StudioCRMDbContext>().UseNpgsql(builder.ConnectionString).Options);
    }
    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase(Environment.GetEnvironmentVariable("TPAY_TEST_DB")!);
        await using var connection = new NpgsqlConnection(database.connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA {database.schema}", connection);
        await command.ExecuteNonQueryAsync();
        try
        {
            await using var context = database.Context();
            await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
            return database;
        }
        catch { await database.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        // Only the randomly named schema created by this test is removed; application data is untouched.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TPAY_TEST_DB")))
            Skip = "Set TPAY_TEST_DB to run isolated PostgreSQL integration tests.";
    }
}
