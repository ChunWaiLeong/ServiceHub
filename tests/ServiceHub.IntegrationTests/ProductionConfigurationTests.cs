using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ServiceHub.Api.Configuration;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class ProductionConfigurationTests
{
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["ConnectionStrings:Database"] = "Host=localhost;Database=unused",
        ["Jwt:Key"] = TestAuthConfiguration.Key,
        ["Jwt:Issuer"] = TestAuthConfiguration.Issuer,
        ["Jwt:Audience"] = TestAuthConfiguration.Audience,
        ["Jwt:ExpirationMinutes"] = "30",
        ["Cors:AllowedOrigins:0"] = "https://servicehub.example"
    };

    [Theory]
    [InlineData("ConnectionStrings:Database", "", "ConnectionStrings:Database")]
    [InlineData("Jwt:Key", "", "Jwt:Key")]
    [InlineData("Jwt:Key", "not-base64", "Jwt:Key")]
    [InlineData("Jwt:Key", "YWJj", "Jwt:Key")]
    [InlineData("Jwt:Issuer", "", "Jwt:Issuer")]
    [InlineData("Jwt:Audience", "", "Jwt:Audience")]
    [InlineData("Jwt:ExpirationMinutes", "0", "Jwt:ExpirationMinutes")]
    [InlineData("Cors:AllowedOrigins:0", null, "Cors:AllowedOrigins")]
    [InlineData("Cors:AllowedOrigins:0", "http://servicehub.example", "Cors:AllowedOrigins")]
    [InlineData("Cors:AllowedOrigins:0", "https://*.vercel.app", "Cors:AllowedOrigins")]
    [InlineData("Cors:AllowedOrigins:0", "https://servicehub.example/", "Cors:AllowedOrigins")]
    [InlineData("Cors:AllowedOrigins:0", "https://servicehub.example/account", "Cors:AllowedOrigins")]
    [InlineData("Cors:AllowedOrigins:0", "https://user:secret@servicehub.example", "Cors:AllowedOrigins")]
    public void InvalidProductionSettings_FailWithConfigurationName(string key, string? value, string expected)
    {
        var settings = ValidSettings();
        settings[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var error = Assert.Throws<InvalidOperationException>(() => ProductionConfiguration.Validate(configuration));
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain(TestAuthConfiguration.Key, error.Message);
        Assert.DoesNotContain("user:secret", error.Message);
    }

    [Fact]
    public void ValidProductionSettings_AcceptMultipleExactOrigins()
    {
        var settings = ValidSettings();
        settings["Cors:AllowedOrigins:1"] = "https://www.servicehub.example";
        ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    [Fact]
    public async Task ProductionStartup_WithValidSettings_ServesHealthAndHidesSwagger()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(ValidSettings()));
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Fact]
    public void ProductionStartup_RejectsMissingDatabaseBeforeServingRequests()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            var settings = ValidSettings();
            settings["ConnectionStrings:Database"] = "";
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        });
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("ConnectionStrings:Database", error.Message);
    }
}
