using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class ReferenceDataSeedingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private async Task<(int ExitCode, string Error)> RunCommandAsync(string command)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "ServiceHub.Api.dll"));
        start.ArgumentList.Add(command);
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["ConnectionStrings__Database"] = fixture.GetConnectionString();
        start.Environment["Jwt__Key"] = TestAuthConfiguration.Key;
        start.Environment["Jwt__Issuer"] = TestAuthConfiguration.Issuer;
        start.Environment["Jwt__Audience"] = TestAuthConfiguration.Audience;
        start.Environment["Cors__AllowedOrigins__0"] = "https://servicehub.example";
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        await output;
        return (process.ExitCode, await error);
    }

    [PostgresFact]
    public async Task ProductionReferenceDataCommand_IsIdempotentAndSeedsOnlyCategories()
    {
        Assert.Equal(0, (await RunCommandAsync("--seed-reference-data")).ExitCode);
        await using var context = fixture.CreateContext();
        var first = await context.BusinessCategories.AsNoTracking().OrderBy(category => category.Name).ToListAsync();
        Assert.Equal(5, first.Count);
        Assert.Equal(0, (await RunCommandAsync("--seed-reference-data")).ExitCode);
        var second = await context.BusinessCategories.AsNoTracking().OrderBy(category => category.Name).ToListAsync();
        Assert.Equal(first.Select(category => (category.Id, category.Name)), second.Select(category => (category.Id, category.Name)));
        Assert.False(await context.Users.AnyAsync());
        Assert.False(await context.Roles.AnyAsync());
        Assert.False(await context.Businesses.AnyAsync());
        Assert.False(await context.Services.AnyAsync());
        Assert.False(await context.Bookings.AnyAsync());
    }

    [PostgresFact]
    public async Task DevelopmentCommand_RemainsForbiddenInProduction()
    {
        var result = await RunCommandAsync("--seed-development");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Development seeding is only allowed in Development.", result.Error);
    }
}
