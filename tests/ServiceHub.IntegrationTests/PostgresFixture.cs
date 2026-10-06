using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceHub.Api.Data;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SERVICEHUB_TEST_DATABASE")))
        {
            Skip = "Set SERVICEHUB_TEST_DATABASE to a dedicated PostgreSQL test database.";
        }
    }
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string? connectionString = Environment.GetEnvironmentVariable("SERVICEHUB_TEST_DATABASE");
    private readonly string schema = "servicehub_test_" + Guid.NewGuid().ToString("N");
    private bool schemaCreated;

    public ApplicationDbContext CreateContext()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("PostgreSQL test database is not configured.");

        var testConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(testConnection.ConnectionString).Options);
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        // The schema name is generated here, never supplied by a user.
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection);
        await command.ExecuteNonQueryAsync();
        schemaCreated = true;
        try
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (!schemaCreated) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
        schemaCreated = false;
    }
}
