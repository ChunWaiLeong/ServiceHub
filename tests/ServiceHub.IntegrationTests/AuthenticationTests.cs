using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ServiceHub.Api.Contracts.Auth;
using ServiceHub.Api.Data;
using ServiceHub.Api.Data.Seeding;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class AuthenticationTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private const string Password = "DisposableTest!123";

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            TestAuthConfiguration.Configure(builder);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Database"] = database.GetConnectionString() }));
        });
        await using var scope = factory.Services.CreateAsyncScope();
        await RoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>());
        return factory;
    }

    private static RegisterRequest NewRegistration(string role = "Customer") => new(
        "Test", "Person", $"auth-{Guid.NewGuid():N}@example.test", Password, role);

    private static async Task<LoginResponse> RegisterAndLoginAsync(HttpClient client, string role = "Customer")
    {
        var request = NewRegistration(role);
        var registered = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    [PostgresTheory]
    [InlineData("Customer")]
    [InlineData("BusinessOwner")]
    public async Task PublicRegistration_SucceedsWithApprovedRole(string role)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var result = await RegisterAndLoginAsync(client, role);
        Assert.Equal(role, result.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        await using var context = database.CreateContext();
        var user = await context.Users.SingleAsync(user => user.Id == result.User.Id);
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal(user.Email!.ToUpperInvariant(), user.NormalizedEmail);
    }

    [PostgresTheory]
    [InlineData("Admin")]
    [InlineData("Unsupported")]
    public async Task PublicRegistration_RejectsPrivilegeEscalation(string role)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var request = NewRegistration(role);
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = database.CreateContext();
        Assert.False(await context.Users.AnyAsync(user => user.Email == request.Email));
    }

    [PostgresFact]
    public async Task DuplicateEmail_IsRejectedAfterNormalization()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var request = NewRegistration();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/register", request with { Email = request.Email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("already", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task IncorrectPasswordAndUnknownEmail_ReturnSameGenericError()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var registration = NewRegistration();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(registration.Email, "WrongPassword!123"));
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("unknown@example.test", Password));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        var wrongProblem = await wrong.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        var unknownProblem = await unknown.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal("Invalid email or password.", wrongProblem?.Title);
        Assert.Equal(wrongProblem?.Title, unknownProblem?.Title);
    }

    [PostgresFact]
    public async Task Jwt_IdentifiesAuthenticatedUser_AndMeIgnoresClientUserId()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var session = await RegisterAndLoginAsync(client);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Equal(session.User.Id.ToString(), jwt.Subject);
        Assert.Equal(session.User.Email, jwt.Claims.Single(claim => claim.Type == "email").Value);
        Assert.Equal("Customer", jwt.Claims.Single(claim => claim.Type == "role").Value);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var response = await client.GetAsync($"/api/auth/me?userId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(session.User, await response.Content.ReadFromJsonAsync<AuthUserResponse>());
    }

    [PostgresFact]
    public async Task Me_RejectsUnauthenticatedRequests()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [PostgresTheory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("BusinessOwner", HttpStatusCode.OK)]
    public async Task BusinessOwnerEndpoint_EnforcesRole(string role, HttpStatusCode expected)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var session = await RegisterAndLoginAsync(client, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        Assert.Equal(expected, (await client.GetAsync("/api/auth/business-owner")).StatusCode);
    }

    [PostgresTheory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expiry")]
    public async Task InvalidJwt_IsRejected(string invalidPart)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var session = await RegisterAndLoginAsync(client);
        var key = invalidPart == "signature" ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) : TestAuthConfiguration.Key;
        var expired = invalidPart == "expiry";
        var token = new JwtSecurityToken(
            invalidPart == "issuer" ? "wrong" : TestAuthConfiguration.Issuer,
            invalidPart == "audience" ? "wrong" : TestAuthConfiguration.Audience,
            [new Claim("sub", session.User.Id.ToString()), new Claim("role", "Customer")],
            DateTime.UtcNow.AddMinutes(-10), expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [PostgresFact]
    public async Task RoleSeeding_IsIdempotent()
    {
        await using var factory = await CreateFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await RoleSeeder.SeedAsync(roles);
        await RoleSeeder.SeedAsync(roles);
        Assert.Equal(3, await roles.Roles.CountAsync());
    }

    [PostgresFact]
    public async Task RepeatedFailedLogins_LockAccountTemporarily()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var request = NewRegistration();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, "WrongPassword!123"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(request.Email, Password))).StatusCode);
    }
}
