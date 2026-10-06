using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceHub.Api.Contracts;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class FoundationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public FoundationTests(WebApplicationFactory<Program> factory)
    {
        client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = "",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
                }));
        }).CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsSuccess_WithoutDatabaseConfiguration()
    {
        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ServiceHub API", health?.Application);
        Assert.Equal("Healthy", health?.Status);
    }

    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task Cors_AllowsOnlyConfiguredOrigins(string origin, bool allowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetails()
    {
        var response = await client.GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem?.Status);
    }

    [Fact]
    public async Task Swagger_DescribesHealthEndpoint()
    {
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        Assert.Contains("/api/health", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnexpectedException_ReturnsSafeProblemDetails()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services => services.AddControllers()
                .AddApplicationPart(typeof(FailingTestController).Assembly));
        });
        var response = await factory.CreateClient().GetAsync("/test/failure");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive test detail", body);
        Assert.Contains("traceId", body);
    }
}

// This endpoint is loaded only by the exception-handling test, never by the API.
[ApiController]
[Route("test/failure")]
public sealed class FailingTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => throw new InvalidOperationException("sensitive test detail");
}
