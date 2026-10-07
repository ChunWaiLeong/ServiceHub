using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceHub.Api.Contracts.Auth;
using ServiceHub.Api.Contracts.Businesses;
using ServiceHub.Api.Contracts.Services;
using ServiceHub.Api.Data.Seeding;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class BusinessManagementTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private static readonly Guid CategoryId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static BusinessRequest NewBusiness() => new("Studio " + Guid.NewGuid().ToString("N"),
        "Thoughtful hair and beauty services.", CategoryId, "10 Example Street, Sydney", null,
        "studio@example.test", "Australia/Sydney");
    private static ServiceRequest NewService() => new("Haircut", "Consultation and cut.", 35.50m, "AUD", 30);

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            TestAuthConfiguration.Configure(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Database"] = database.GetConnectionString() }));
        });
        await using var scope = factory.Services.CreateAsyncScope();
        await RoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>());
        await using var context = database.CreateContext();
        await DevelopmentDataSeeder.SeedAsync(context, CancellationToken.None);
        return factory;
    }

    private static async Task<(HttpClient Client, Guid UserId)> SignInAsync(WebApplicationFactory<Program> factory, string role = "BusinessOwner")
    {
        var client = factory.CreateClient();
        var registration = new RegisterRequest("Test", "Owner", $"business-{Guid.NewGuid():N}@example.test", "DisposableTest!123", role);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(registration.Email, registration.Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return (client, session.User.Id);
    }
    private static async Task<BusinessResponse> CreateBusinessAsync(HttpClient client, BusinessRequest? input = null)
    {
        var response = await client.PostAsJsonAsync("/api/businesses", input ?? NewBusiness());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<BusinessResponse>())!;
    }
    private static async Task<ServiceResponse> CreateServiceAsync(HttpClient client, Guid businessId)
    {
        var response = await client.PostAsJsonAsync($"/api/businesses/{businessId}/services", NewService());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<ServiceResponse>())!;
    }

    [PostgresFact]
    public async Task OwnerCreatesBusiness_UsingClaimsAndServerControlledFields()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, userId) = await SignInAsync(factory);
        using var client = owner;
        var input = NewBusiness();
        var response = await client.PostAsJsonAsync("/api/businesses", new
        {
            input.Name, input.Description, input.BusinessCategoryId, input.Address, input.ContactPhone,
            input.ContactEmail, input.TimeZoneId, OwnerId = Guid.NewGuid(), IsActive = false,
            CreatedAtUtc = DateTime.UnixEpoch, UpdatedAtUtc = DateTime.UnixEpoch, Id = Guid.NewGuid()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var business = (await response.Content.ReadFromJsonAsync<BusinessResponse>())!;
        await using var context = database.CreateContext();
        var stored = await context.Businesses.SingleAsync(b => b.Id == business.Id);
        Assert.Equal(userId, stored.OwnerId);
        Assert.True(stored.IsActive);
        Assert.NotEqual(DateTime.UnixEpoch, stored.CreatedAtUtc);
        var mine = await client.GetFromJsonAsync<BusinessResponse>("/api/owner/business");
        Assert.Equal(business, mine);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/businesses", NewBusiness())).StatusCode);
    }

    [PostgresFact]
    public async Task ConcurrentBusinessCreation_StillEnforcesOneBusinessPerOwner()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, userId) = await SignInAsync(factory);
        using var client = owner;
        var results = await Task.WhenAll(client.PostAsJsonAsync("/api/businesses", NewBusiness()), client.PostAsJsonAsync("/api/businesses", NewBusiness()));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var context = database.CreateContext();
        Assert.Equal(1, await context.Businesses.CountAsync(b => b.OwnerId == userId));
    }

    [PostgresFact]
    public async Task OwnerUpdatesBusiness_WithoutChangingOwnershipOrCreationDate()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, userId) = await SignInAsync(factory);
        using var client = owner;
        var business = await CreateBusinessAsync(client);
        var input = NewBusiness() with { Name = "Updated Studio", ContactPhone = " 0400 123 456 " };
        var response = await client.PutAsJsonAsync($"/api/businesses/{business.Id}", new
        {
            input.Name, input.Description, input.BusinessCategoryId, input.Address, input.ContactPhone,
            input.ContactEmail, input.TimeZoneId, OwnerId = Guid.NewGuid(), CreatedAtUtc = DateTime.UnixEpoch, IsActive = false
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<BusinessResponse>())!;
        Assert.Equal("Updated Studio", updated.Name);
        Assert.Equal("0400 123 456", updated.ContactPhone);
        Assert.Equal(business.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc > business.UpdatedAtUtc);
        Assert.True(updated.IsActive);
        await using var context = database.CreateContext();
        Assert.Equal(userId, (await context.Businesses.SingleAsync(b => b.Id == business.Id)).OwnerId);
    }

    [PostgresFact]
    public async Task AnotherOwnerCannotUpdateBusiness_AndGetsConcealedNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var business = await CreateBusinessAsync(client);
        var (stranger, _) = await SignInAsync(factory); using var other = stranger;
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/businesses/{business.Id}", NewBusiness())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/owner/business")).StatusCode);
        Assert.Equal(business.Name, (await client.GetFromJsonAsync<BusinessResponse>("/api/owner/business"))!.Name);
    }

    [PostgresFact]
    public async Task CustomerCannotCreateOrManageBusinessOrServices()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var ownerClient = owner;
        var business = await CreateBusinessAsync(ownerClient);
        var service = await CreateServiceAsync(ownerClient, business.Id);
        var (customer, _) = await SignInAsync(factory, "Customer"); using var client = customer;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/businesses", NewBusiness())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/businesses/{business.Id}", NewBusiness())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/owner/business")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/owner/business/services")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/businesses/{business.Id}/services", NewService())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/businesses/{business.Id}/services/{service.Id}", NewService())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/api/businesses/{business.Id}/services/{service.Id}/status", new ServiceStatusRequest(false))).StatusCode);
    }

    [PostgresFact]
    public async Task OwnerWritesRequireAuthentication()
    {
        await using var factory = await CreateFactoryAsync(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/businesses", NewBusiness())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/owner/business")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/businesses/{Guid.NewGuid()}/services", NewService())).StatusCode);
    }

    [PostgresTheory]
    [InlineData("category")]
    [InlineData("timezone")]
    [InlineData("name")]
    [InlineData("email")]
    public async Task InvalidBusinessInput_IsRejected(string field)
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var input = NewBusiness();
        input = field switch
        {
            "category" => input with { BusinessCategoryId = Guid.NewGuid() },
            "timezone" => input with { TimeZoneId = "Not/AZone" },
            "name" => input with { Name = "   " },
            _ => input with { ContactEmail = "invalid" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/businesses", input)).StatusCode);
    }

    [PostgresFact]
    public async Task PublicDiscovery_FiltersPagesAndCountsOnlyActiveServices()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var name = "FindMe " + Guid.NewGuid().ToString("N");
        var business = await CreateBusinessAsync(client, NewBusiness() with { Name = name });
        await CreateServiceAsync(client, business.Id);
        var hidden = await CreateServiceAsync(client, business.Id);
        await client.PatchAsJsonAsync($"/api/businesses/{business.Id}/services/{hidden.Id}/status", new ServiceStatusRequest(false));
        using var publicClient = factory.CreateClient();
        var page = (await publicClient.GetFromJsonAsync<BusinessPageResponse>($"/api/businesses?search={name.ToLowerInvariant()}&categoryId={CategoryId}&page=1&pageSize=1"))!;
        Assert.Equal(1, page.TotalCount); Assert.Single(page.Items); Assert.Equal(business.Id, page.Items[0].Id);
        Assert.Equal(1, page.Items[0].ActiveServiceCount);
        Assert.Empty((await publicClient.GetFromJsonAsync<BusinessPageResponse>($"/api/businesses?search={name}&page=2&pageSize=1"))!.Items);
        Assert.Empty((await publicClient.GetFromJsonAsync<BusinessPageResponse>($"/api/businesses?search={name}&categoryId={Guid.NewGuid()}"))!.Items);
        Assert.Equal(business, await publicClient.GetFromJsonAsync<BusinessResponse>($"/api/businesses/{business.Id}"));
        Assert.NotEmpty((await publicClient.GetFromJsonAsync<CategoryResponse[]>("/api/categories"))!);
        Assert.Contains((await publicClient.GetFromJsonAsync<TimeZoneResponse[]>("/api/time-zones"))!, z => z.Id == "Australia/Sydney");
    }

    [PostgresFact]
    public async Task SearchTreatsWildcardCharactersLiterally()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var unique = Guid.NewGuid().ToString("N");
        await CreateBusinessAsync(client, NewBusiness() with { Name = unique + " 100%_studio" });
        using var publicClient = factory.CreateClient();
        var matches = await publicClient.GetFromJsonAsync<BusinessPageResponse>("/api/businesses?search=" + Uri.EscapeDataString(unique + " 100%_"));
        Assert.Single(matches!.Items);
        var absent = await publicClient.GetFromJsonAsync<BusinessPageResponse>("/api/businesses?search=" + Uri.EscapeDataString(unique + " 100%_x"));
        Assert.Empty(absent!.Items);
    }

    [PostgresTheory]
    [InlineData("page=0")]
    [InlineData("pageSize=51")]
    [InlineData("page=100001")]
    public async Task InvalidPaging_IsRejected(string query)
    {
        await using var factory = await CreateFactoryAsync(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/businesses?" + query)).StatusCode);
    }

    [PostgresFact]
    public async Task InactiveBusiness_IsHiddenIncludingItsServices_ButVisibleToItsOwner()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var business = await CreateBusinessAsync(client); var service = await CreateServiceAsync(client, business.Id);
        await using (var context = database.CreateContext())
        {
            var stored = await context.Businesses.SingleAsync(b => b.Id == business.Id);
            stored.IsActive = false; await context.SaveChangesAsync();
        }
        using var publicClient = factory.CreateClient();
        Assert.Empty((await publicClient.GetFromJsonAsync<BusinessPageResponse>("/api/businesses?search=" + business.Name))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/businesses/{business.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/businesses/{business.Id}/services")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"/api/businesses/{business.Id}/services/{service.Id}")).StatusCode);
        Assert.False((await client.GetFromJsonAsync<BusinessResponse>("/api/owner/business"))!.IsActive);
    }

    [PostgresFact]
    public async Task ServiceLifecycle_UpdatesDeactivatesAndReactivatesWithoutDeletion()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var business = await CreateBusinessAsync(client); var service = await CreateServiceAsync(client, business.Id);
        var url = $"/api/businesses/{business.Id}/services/{service.Id}";
        var update = await client.PutAsJsonAsync(url, NewService() with { Name = "Signature cut", Price = 45m, DurationMinutes = 45 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<ServiceResponse>())!;
        Assert.Equal("Signature cut", updated.Name); Assert.Equal(45m, updated.Price); Assert.Equal(45, updated.DurationMinutes);
        Assert.Equal(service.CreatedAtUtc, updated.CreatedAtUtc); Assert.True(updated.UpdatedAtUtc > service.UpdatedAtUtc);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(url + "/status", new ServiceStatusRequest(false))).StatusCode);
        using var publicClient = factory.CreateClient();
        Assert.Empty((await publicClient.GetFromJsonAsync<ServiceResponse[]>($"/api/businesses/{business.Id}/services"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync(url)).StatusCode);
        var managed = (await client.GetFromJsonAsync<ServiceResponse[]>("/api/owner/business/services"))!;
        Assert.Single(managed); Assert.False(managed[0].IsActive);
        await using var context = database.CreateContext();
        Assert.True(await context.Services.AnyAsync(s => s.Id == service.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(url + "/status", new ServiceStatusRequest(true))).StatusCode);
        Assert.Single((await publicClient.GetFromJsonAsync<ServiceResponse[]>($"/api/businesses/{business.Id}/services"))!);
    }

    [PostgresTheory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("status")]
    public async Task AnotherOwnerCannotManageServices(string action)
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var business = await CreateBusinessAsync(client); var service = await CreateServiceAsync(client, business.Id);
        var (stranger, _) = await SignInAsync(factory); using var other = stranger;
        var ownBusiness = await CreateBusinessAsync(other);
        var url = $"/api/businesses/{business.Id}/services";
        var response = action switch
        {
            "create" => await other.PostAsJsonAsync(url, NewService()),
            "update" => await other.PutAsJsonAsync(url + "/" + service.Id, NewService()),
            _ => await other.PatchAsJsonAsync(url + "/" + service.Id + "/status", new ServiceStatusRequest(false))
        };
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        // An owned business ID does not authorize a service taken from another business.
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/businesses/{ownBusiness.Id}/services/{service.Id}", NewService())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync($"/api/businesses/{ownBusiness.Id}/services/{service.Id}/status", new ServiceStatusRequest(false))).StatusCode);
    }

    [PostgresFact]
    public async Task ServiceCreation_IgnoresForgedBusinessStatusAndTimestamps()
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var business = await CreateBusinessAsync(client);
        var response = await client.PostAsJsonAsync($"/api/businesses/{business.Id}/services", new
        {
            Name = "Cut", Description = "Cut and style", Price = 35m, Currency = "AUD", DurationMinutes = 30,
            BusinessId = Guid.NewGuid(), IsActive = false, CreatedAtUtc = DateTime.UnixEpoch
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = (await response.Content.ReadFromJsonAsync<ServiceResponse>())!;
        Assert.Equal(business.Id, service.BusinessId); Assert.True(service.IsActive);
        Assert.NotEqual(DateTime.UnixEpoch, service.CreatedAtUtc);
    }

    [PostgresTheory]
    [InlineData("zero-price")]
    [InlineData("negative-price")]
    [InlineData("precision")]
    [InlineData("zero-duration")]
    [InlineData("long-duration")]
    [InlineData("currency")]
    [InlineData("name")]
    public async Task InvalidServiceInput_IsRejected(string field)
    {
        await using var factory = await CreateFactoryAsync();
        var (owner, _) = await SignInAsync(factory); using var client = owner;
        var business = await CreateBusinessAsync(client);
        var input = field switch
        {
            "zero-price" => NewService() with { Price = 0 },
            "negative-price" => NewService() with { Price = -1 },
            "precision" => NewService() with { Price = 35.555m },
            "zero-duration" => NewService() with { DurationMinutes = 0 },
            "long-duration" => NewService() with { DurationMinutes = 481 },
            "currency" => NewService() with { Currency = "USD" },
            _ => NewService() with { Name = "   " }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/businesses/{business.Id}/services", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/businesses/{business.Id}/services/{Guid.NewGuid()}/status", new { })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ServiceResponse[]>("/api/owner/business/services"))!);
    }
}
