using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ServiceHub.Api.Contracts.Auth;
using ServiceHub.Api.Contracts.Bookings;
using ServiceHub.Api.Data;
using ServiceHub.Api.Data.Seeding;
using ServiceHub.Api.Models;
using ServiceHub.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace ServiceHub.IntegrationTests;

public sealed class BookingTests(PostgresFixture database, ITestOutputHelper output) : IClassFixture<PostgresFixture>
{
    private static readonly DateTime Start = new(2030, 1, 6, 22, 0, 0, DateTimeKind.Utc); // Sydney Monday 09:00
    private sealed record Actor(HttpClient Client, Guid Id);
    private sealed record Scenario(WebApplicationFactory<Program> Factory, Actor Owner, Actor Customer, Business Business, Service Service) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { Owner.Client.Dispose(); Customer.Client.Dispose(); await Factory.DisposeAsync(); }
    }
    private sealed class FixedClock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
    private BookingService Service(ApplicationDbContext context, DateTime now)
    {
        var clock = new FixedClock(now);
        return new(context, new AvailabilityService(context, clock), new BusinessService(context, clock), clock);
    }
    private async Task<Actor> ActorAsync(WebApplicationFactory<Program> factory, string role)
    {
        var client = factory.CreateClient();
        var email = $"booking-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Booking", role, email, "DisposableTest!123", role))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "DisposableTest!123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        await using var context = database.CreateContext();
        return new(client, (await context.Users.SingleAsync(u => u.Email == email)).Id);
    }
    private async Task<Scenario> SetupAsync(DbCommandInterceptor? barrier = null)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development"); TestAuthConfiguration.Configure(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:Database"] = database.GetConnectionString() }));
            if (barrier is not null) builder.ConfigureServices(services => services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(barrier)));
        });
        await using (var scope = factory.Services.CreateAsyncScope())
            await RoleSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>());
        var owner = await ActorAsync(factory, "BusinessOwner"); var customer = await ActorAsync(factory, "Customer");
        await using var context = database.CreateContext();
        await DevelopmentDataSeeder.SeedAsync(context, CancellationToken.None);
        var business = new Business { OwnerId = owner.Id, BusinessCategoryId = Guid.Parse("c1000000-0000-0000-0000-000000000001"),
            Name = "Booking studio", Description = "Test studio", Address = "Sydney", ContactEmail = "studio@example.test", TimeZoneId = "Australia/Sydney" };
        var service = new Service { BusinessId = business.Id, Name = "Original haircut", Description = "Test", Price = 35.50m, Currency = "AUD", DurationMinutes = 30 };
        context.AddRange(business, service, new BusinessWorkingHours { BusinessId = business.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new(9,0), EndTime = new(12,0) });
        await context.SaveChangesAsync();
        return new(factory, owner, customer, business, service);
    }
    private static Task<HttpResponseMessage> CreateAsync(Scenario s, DateTime? start = null, HttpClient? client = null)
        => (client ?? s.Customer.Client).PostAsJsonAsync("/api/bookings", new { serviceId = s.Service.Id, startUtc = (start ?? Start).ToString("O") });
    private static async Task<BookingResponse> CreatedAsync(Scenario s, DateTime? start = null)
    {
        var response = await CreateAsync(s, start); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location); return (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
    }
    private async Task<Booking> SeedBookingAsync(Scenario s, DateTime start, BookingStatus status = BookingStatus.Confirmed)
    {
        await using var context = database.CreateContext();
        var b = new Booking { CustomerId = s.Customer.Id, BusinessId = s.Business.Id, ServiceId = s.Service.Id,
            StartUtc = start, EndUtc = start.AddMinutes(30), Status = status, ServiceName = s.Service.Name,
            ServicePrice = s.Service.Price, ServiceCurrency = "AUD", ServiceDurationMinutes = 30 };
        context.Bookings.Add(b); await context.SaveChangesAsync(); return b;
    }

    [PostgresFact]
    public async Task Create_UsesClaimsAndServerValues_PreservesSnapshot_AndBusinessZone()
    {
        await using var s = await SetupAsync();
        var response = await s.Customer.Client.PostAsJsonAsync("/api/bookings", new { serviceId = s.Service.Id, startUtc = Start.ToString("O"),
            customerId = s.Owner.Id, businessId = Guid.NewGuid(), endUtc = Start.AddHours(3), status = "Completed",
            serviceName = "Forged", servicePrice = 1, serviceDurationMinutes = 480, createdAtUtc = Start });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
        Assert.Equal(s.Business.Id, result.BusinessId); Assert.Equal(Start.AddMinutes(30), result.EndUtc);
        Assert.Equal("Confirmed", result.Status); Assert.Null(result.Customer); Assert.Null(result.CancelledAtUtc);
        Assert.Equal("Australia/Sydney", result.TimeZoneId);
        Assert.Equal(new TimeOnly(9,0), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(result.StartUtc, TimeZoneInfo.FindSystemTimeZoneById(result.TimeZoneId))));
        await using var context = database.CreateContext();
        var stored = await context.Bookings.FindAsync(result.Id); Assert.Equal(s.Customer.Id, stored!.CustomerId);
        Assert.True(stored.CreatedAtUtc < Start);
        var service = await context.Services.FindAsync(s.Service.Id); service!.Name = "Changed"; service.Price = 90; service.DurationMinutes = 60; await context.SaveChangesAsync();
        var historical = (await s.Customer.Client.GetFromJsonAsync<BookingResponse>($"/api/bookings/{result.Id}"))!;
        Assert.Equal("Original haircut", historical.ServiceName); Assert.Equal(35.50m, historical.ServicePrice);
        Assert.Equal(30, historical.ServiceDurationMinutes); Assert.Equal("AUD", historical.ServiceCurrency);
    }
    [PostgresTheory]
    [InlineData("owner",403)] [InlineData("anonymous",401)]
    public async Task Create_RequiresCustomer(string actor, int status)
    {
        await using var s = await SetupAsync(); using var anonymous = s.Factory.CreateClient();
        Assert.Equal(status, (int)(await CreateAsync(s, client: actor == "owner" ? s.Owner.Client : anonymous)).StatusCode);
    }
    [PostgresTheory]
    [InlineData("service")] [InlineData("business")] [InlineData("owner")] [InlineData("customer")]
    public async Task Create_RejectsInactiveResources(string target)
    {
        await using var s = await SetupAsync(); await using var context = database.CreateContext();
        if (target == "service") await context.Services.Where(x => x.Id == s.Service.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        else if (target == "business") await context.Businesses.Where(x => x.Id == s.Business.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        else await context.Users.Where(x => x.Id == (target == "owner" ? s.Owner.Id : s.Customer.Id)).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        Assert.Equal(target == "customer" ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound, (await CreateAsync(s)).StatusCode);
    }
    [PostgresTheory]
    [InlineData("2020-01-06T00:00:00Z")] [InlineData("2030-01-06T21:00:00Z")]
    [InlineData("2030-01-06T22:07:00Z")] [InlineData("2030-01-07T01:00:00Z")]
    [InlineData("2030-01-06T22:00:00")] [InlineData("2030-01-07T09:00:00+11:00")]
    public async Task Create_RejectsPastOutsideHoursOffGridOrNonUtc(string value)
    {
        await using var s = await SetupAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Customer.Client.PostAsJsonAsync("/api/bookings", new { serviceId = s.Service.Id, startUtc = value })).StatusCode);
    }
    [PostgresFact]
    public async Task Create_RejectsClosure_AndUnavailableService()
    {
        await using var s = await SetupAsync(); await using var context = database.CreateContext();
        context.BusinessBlockedPeriods.Add(new BusinessBlockedPeriod { BusinessId = s.Business.Id, StartUtc = Start, EndUtc = Start.AddMinutes(30) }); await context.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateAsync(s)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Customer.Client.PostAsJsonAsync("/api/bookings", new { serviceId = Guid.NewGuid(), startUtc = Start.ToString("O") })).StatusCode);
    }
    [PostgresFact]
    public async Task Booking_RemovesSlots_AdjacentAccepted_CancellationRestoresSlot()
    {
        await using var s = await SetupAsync(); var b = await CreatedAsync(s);
        Assert.Equal(HttpStatusCode.Conflict, (await CreateAsync(s, Start.AddMinutes(15))).StatusCode);
        await CreatedAsync(s, Start.AddMinutes(30));
        await using var context = database.CreateContext(); var a = new AvailabilityService(context, new FixedClock(Start.AddDays(-1)));
        Assert.DoesNotContain((await a.GetAsync(s.Business.Id, s.Service.Id, new(2030,1,7), default)).Slots, x => x.StartUtc == Start);
        var cancel = await s.Customer.Client.PostAsync($"/api/bookings/{b.Id}/cancel", null); Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = (await cancel.Content.ReadFromJsonAsync<BookingResponse>())!; Assert.Equal("Cancelled", cancelled.Status); Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Contains((await a.GetAsync(s.Business.Id, s.Service.Id, new(2030,1,7), default)).Slots, x => x.StartUtc == Start);
        await CreatedAsync(s); // Cancelled history does not prevent a new reservation.
    }
    [PostgresFact]
    public async Task CustomerReadsAndCancellation_ConcealOtherCustomers_AndSortUpcomingThenHistory()
    {
        await using var s = await SetupAsync(); var later = await CreatedAsync(s, Start.AddHours(1)); var earlier = await CreatedAsync(s);
        var past = await SeedBookingAsync(s, DateTime.UtcNow.AddDays(-2), BookingStatus.Completed);
        var other = await ActorAsync(s.Factory, "Customer"); using var otherClient = other.Client;
        Assert.Empty((await otherClient.GetFromJsonAsync<BookingResponse[]>("/api/me/bookings"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/bookings/{earlier.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PostAsync($"/api/bookings/{earlier.Id}/cancel", null)).StatusCode);
        var mine = (await s.Customer.Client.GetFromJsonAsync<BookingResponse[]>("/api/me/bookings"))!;
        Assert.Equal(new[] { earlier.Id, later.Id, past.Id }, mine.Select(x => x.Id));
        Assert.All(mine, x => Assert.Null(x.Customer));
    }
    [PostgresTheory]
    [InlineData("Confirmed",-1)] [InlineData("Cancelled",1)] [InlineData("Completed",1)]
    public async Task CustomerCancellation_RejectsStartedOrTerminalBookings(string status, int days)
    {
        await using var s = await SetupAsync(); var b = await SeedBookingAsync(s, DateTime.UtcNow.AddDays(days), Enum.Parse<BookingStatus>(status));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Customer.Client.PostAsync($"/api/bookings/{b.Id}/cancel", null)).StatusCode);
    }
    [PostgresFact]
    public async Task OwnerReadsAndActions_AreScopedToBusiness_AndIncludeOnlyUsefulCustomerData()
    {
        await using var s = await SetupAsync(); var b = await CreatedAsync(s);
        await using var otherBusiness = await SetupAsync(); var otherClient = otherBusiness.Owner.Client;
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/bookings/{b.Id}")).StatusCode);
        foreach (var suffix in new[] { "", "/cancel", "/complete" })
        {
            var route = $"/api/owner/business/bookings/{b.Id}{suffix}";
            var response = suffix == "" ? await otherClient.GetAsync(route) : await otherClient.PostAsync(route, null);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Customer.Client.GetAsync("/api/owner/business/bookings")).StatusCode);
        var result = (await s.Owner.Client.GetFromJsonAsync<BookingResponse>($"/api/owner/business/bookings/{b.Id}"))!;
        Assert.Equal("Booking Customer", result.Customer!.Name); Assert.EndsWith("@example.test", result.Customer.Email);
        Assert.Equal(b.Id, (await s.Owner.Client.GetFromJsonAsync<BookingResponse[]>(("/api/owner/business/bookings?status=Confirmed")))!.Single().Id);
        Assert.Equal(HttpStatusCode.OK, (await s.Owner.Client.PostAsync($"/api/owner/business/bookings/{b.Id}/cancel", null)).StatusCode);
        Assert.Empty((await s.Owner.Client.GetFromJsonAsync<BookingResponse[]>("/api/owner/business/bookings?status=Confirmed"))!);
        Assert.Single((await s.Owner.Client.GetFromJsonAsync<BookingResponse[]>("/api/owner/business/bookings?status=Cancelled"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Owner.Client.GetAsync("/api/owner/business/bookings?status=Unknown")).StatusCode);
        Assert.Empty((await s.Owner.Client.GetFromJsonAsync<BookingResponse[]>("/api/owner/business/bookings?fromUtc=2031-01-01T00:00:00Z"))!);
    }
    [PostgresTheory]
    [InlineData("Confirmed",-1,"complete",200)] [InlineData("Confirmed",1,"complete",400)]
    [InlineData("Cancelled",-1,"complete",400)] [InlineData("Completed",-1,"complete",400)]
    [InlineData("Cancelled",1,"cancel",400)] [InlineData("Completed",-1,"cancel",400)]
    [InlineData("Confirmed",-1,"cancel",200)]
    public async Task OwnerTransitions_EnforceStateAndEndTime(string status, int days, string action, int expected)
    {
        await using var s = await SetupAsync(); var b = await SeedBookingAsync(s, DateTime.UtcNow.AddDays(days), Enum.Parse<BookingStatus>(status));
        var response = await s.Owner.Client.PostAsync($"/api/owner/business/bookings/{b.Id}/{action}", null);
        Assert.Equal(expected, (int)response.StatusCode);
        if (expected == 200)
        {
            Assert.Equal(action == "complete" ? "Completed" : "Cancelled", (await response.Content.ReadFromJsonAsync<BookingResponse>())!.Status);
            Assert.Equal(HttpStatusCode.BadRequest, (await s.Owner.Client.PostAsync($"/api/owner/business/bookings/{b.Id}/{action}", null)).StatusCode);
        }
    }
    [PostgresFact]
    public async Task CompletionBoundary_UsesInjectedClock_AndTerminalTransitionsAreAtomic()
    {
        await using var s = await SetupAsync(); var b = await SeedBookingAsync(s, Start);
        await using var context = database.CreateContext(); var service = Service(context, b.EndUtc);
        var result = await service.CompleteOwnerAsync(s.Owner.Id, b.Id, default);
        Assert.Equal("Completed", result.Status); Assert.Null(result.CancelledAtUtc);
        var a = new AvailabilityService(context, new FixedClock(Start.AddDays(-1)));
        Assert.Contains((await a.GetAsync(s.Business.Id, s.Service.Id, new(2030,1,7), default)).Slots, x => x.StartUtc == Start);
    }
    public sealed class InsertBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public int Arrivals => arrivals;
        public ConcurrentBag<string> FailureSqlStates { get; } = [];
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"Bookings\"", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"Bookings\"", StringComparison.Ordinal)
                && eventData.Exception is PostgresException postgres)
                FailureSqlStates.Add(postgres.SqlState);
            return Task.CompletedTask;
        }
    }
    [PostgresFact]
    public async Task ConcurrentRequests_BothPassAvailability_OnlyOneCommits_AndDatabaseConflictBecomes409()
    {
        var barrier = new InsertBarrier(); await using var s = await SetupAsync(barrier);
        var other = await ActorAsync(s.Factory, "Customer"); using var otherClient = other.Client;
        var responses = await Task.WhenAll(CreateAsync(s), CreateAsync(s, client: otherClient));
        Assert.Equal(2, barrier.Arrivals); // Both independent contexts reached INSERT after availability validation.
        Assert.Equal(new[] { 201,409 }, responses.Select(x => (int)x.StatusCode).OrderBy(x => x));
        var sqlState = Assert.Single(barrier.FailureSqlStates);
        Assert.Contains(sqlState, new[] { PostgresErrorCodes.ExclusionViolation, PostgresErrorCodes.DeadlockDetected });
        output.WriteLine("Observed competing insert SQLSTATE: " + sqlState);
        var conflict = await responses.Single(x => x.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync();
        Assert.Contains("no longer available", conflict); Assert.DoesNotContain("23P01", conflict); Assert.DoesNotContain("EX_Bookings", conflict);
        Assert.DoesNotContain("40P01", conflict); Assert.DoesNotContain("deadlock", conflict.ToLowerInvariant());
        await using var context = database.CreateContext(); Assert.Equal(1, await context.Bookings.CountAsync(x => x.BusinessId == s.Business.Id));
    }
    private sealed class InsertFailure(string sqlState, string? constraintName) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"Bookings\"", StringComparison.Ordinal))
                throw new PostgresException("Internal database detail", "ERROR", "ERROR", sqlState, constraintName: constraintName);
            return ValueTask.FromResult(result);
        }
    }

    [PostgresTheory]
    [InlineData(PostgresErrorCodes.DeadlockDetected, null, 409)]
    [InlineData(PostgresErrorCodes.ExclusionViolation, BookingService.OverlapConstraint, 409)]
    [InlineData(PostgresErrorCodes.ExclusionViolation, "unrelated_constraint", 500)]
    [InlineData(PostgresErrorCodes.UniqueViolation, "unrelated_constraint", 500)]
    [InlineData(PostgresErrorCodes.SerializationFailure, null, 500)]
    public async Task BookingInsertFailure_MapsOnlyExpectedConflictsToSafeProblemDetails(string sqlState, string? constraint, int expected)
    {
        await using var s = await SetupAsync(new InsertFailure(sqlState, constraint));
        var response = await CreateAsync(s);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        if (expected == 409) Assert.Contains("no longer available", body);
        Assert.DoesNotContain(sqlState, body);
        Assert.DoesNotContain("Internal database detail", body);
        if (constraint is not null) Assert.DoesNotContain(constraint, body);
        await using var context = database.CreateContext();
        Assert.False(await context.Bookings.AnyAsync(b => b.BusinessId == s.Business.Id));
    }

    [PostgresFact]
    public async Task DatabaseConstraint_IsBusinessScoped_HalfOpen_AndConfirmedOnly()
    {
        await using var s = await SetupAsync(); var first = await SeedBookingAsync(s, Start);
        await SeedBookingAsync(s, Start, BookingStatus.Cancelled); await SeedBookingAsync(s, Start, BookingStatus.Completed);
        await SeedBookingAsync(s, first.EndUtc);
        await using var anotherBusiness = await SetupAsync(); await SeedBookingAsync(anotherBusiness, Start);
        await using var context = database.CreateContext();
        context.Bookings.Add(new Booking { CustomerId = s.Customer.Id, BusinessId = s.Business.Id, ServiceId = s.Service.Id,
            StartUtc = Start.AddMinutes(15), EndUtc = Start.AddMinutes(45), ServiceName = "Test", ServicePrice = 1, ServiceCurrency = "AUD", ServiceDurationMinutes = 30 });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgres.SqlState); Assert.Equal(BookingService.OverlapConstraint, postgres.ConstraintName);
    }
    [PostgresFact]
    public async Task OverlappingServices_ShareOneBusinessCalendar()
    {
        await using var s = await SetupAsync(); await CreatedAsync(s);
        await using var context = database.CreateContext();
        var second = new Service { BusinessId = s.Business.Id, Name = "Second service", Description = "Test", Price = 25, Currency = "AUD", DurationMinutes = 60 };
        context.Services.Add(second); await context.SaveChangesAsync();
        var response = await s.Customer.Client.PostAsJsonAsync("/api/bookings", new { serviceId = second.Id, startUtc = Start.ToString("O") });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    [PostgresFact]
    public async Task SimultaneousCancelAndComplete_OnlyOneTransitionSucceeds()
    {
        await using var s = await SetupAsync(); var b = await SeedBookingAsync(s, Start);
        await using var first = database.CreateContext(); await using var second = database.CreateContext();
        async Task<bool> Attempt(Func<Task<BookingResponse>> action)
        {
            try { await action(); return true; }
            catch (ServiceHub.Api.ErrorHandling.RequestException ex) { Assert.Equal(400, ex.StatusCode); return false; }
        }
        var outcomes = await Task.WhenAll(Attempt(() => Service(first, b.EndUtc).CancelOwnerAsync(s.Owner.Id,b.Id,default)),
            Attempt(() => Service(second, b.EndUtc).CompleteOwnerAsync(s.Owner.Id,b.Id,default)));
        Assert.Single(outcomes, x => x);
        await using var check = database.CreateContext(); var stored = await check.Bookings.FindAsync(b.Id);
        Assert.True(stored!.Status is BookingStatus.Completed or BookingStatus.Cancelled);
        Assert.Equal(stored.Status == BookingStatus.Cancelled, stored.CancelledAtUtc.HasValue);
    }
    [PostgresFact]
    public async Task CustomerCannotCancelAtExactStart_UsesInjectedClock()
    {
        await using var s = await SetupAsync(); var b = await SeedBookingAsync(s, Start);
        await using var context = database.CreateContext();
        var ex = await Assert.ThrowsAsync<ServiceHub.Api.ErrorHandling.RequestException>(() => Service(context, Start).CancelCustomerAsync(s.Customer.Id,b.Id,default));
        Assert.Equal(400, ex.StatusCode);
    }
    [PostgresFact]
    public async Task Migration_RollsBackAndReappliesConstraint_WithoutDroppingSharedExtension()
    {
        var isolated = new PostgresFixture(); await isolated.InitializeAsync();
        try
        {
            await using var context = isolated.CreateContext();
            var migrator = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("20261006142457_InitialServiceHub");
            await context.Database.OpenConnectionAsync();
            async Task<long> ConstraintCount()
            {
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT count(*) FROM pg_constraint WHERE conrelid = '\"Bookings\"'::regclass AND conname = 'EX_Bookings_ConfirmedBusinessOverlap'";
                return (long)(await command.ExecuteScalarAsync())!;
            }
            Assert.Equal(0, await ConstraintCount());
            await migrator.MigrateAsync(); Assert.Equal(1, await ConstraintCount());
        }
        finally { await isolated.DisposeAsync(); }
    }

}
