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
using ServiceHub.Api.Contracts.Availability;
using ServiceHub.Api.Contracts.Businesses;
using ServiceHub.Api.Contracts.Services;
using ServiceHub.Api.Data;
using ServiceHub.Api.Data.Seeding;
using ServiceHub.Api.ErrorHandling;
using ServiceHub.Api.Models;
using ServiceHub.Api.Services;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class AvailabilityTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private static readonly Guid CategoryId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly DateOnly Monday = new(2030, 1, 7);
    private sealed class FixedClock(DateTime utc) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(utc); }
    private static readonly TimeProvider BeforeTestDate = new FixedClock(new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    private static WeeklyHoursRequest Week(params (int Day, string Start, string End)[] intervals) => new(
        new[] { 1, 2, 3, 4, 5, 6, 0 }.Select(day => new WorkingDayRequest(day,
            intervals.Where(i => i.Day == day).Select(i => new WorkingIntervalRequest(TimeOnly.Parse(i.Start), TimeOnly.Parse(i.End))).ToList())).ToList());
    private static DateTime Utc(DateOnly date, string time, string zone = "Australia/Sydney")
        => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.Parse(time), DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById(zone));

    private async Task<WebApplicationFactory<Program>> FactoryAsync()
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
    private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> factory, string role = "BusinessOwner")
    {
        var client = factory.CreateClient();
        var registration = new RegisterRequest("Availability", "Test", $"availability-{Guid.NewGuid():N}@example.test", "DisposableTest!123", role);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(registration.Email, registration.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }
    private static async Task<BusinessResponse> ApiBusinessAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/businesses", new BusinessRequest("Availability studio", "A test studio", CategoryId,
            "10 Example Street", null, "studio@example.test", "Australia/Sydney"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BusinessResponse>())!;
    }
    private async Task<(Business Business, Service Service, Guid CustomerId)> GraphAsync(ApplicationDbContext context, int duration = 30, string zone = "Australia/Sydney")
    {
        await DevelopmentDataSeeder.SeedAsync(context, CancellationToken.None);
        var owner = new ApplicationUser { FirstName = "Owner", LastName = "Test" };
        var customer = new ApplicationUser { FirstName = "Customer", LastName = "Test" };
        var business = new Business { OwnerId = owner.Id, BusinessCategoryId = CategoryId, Name = "Slots " + Guid.NewGuid(),
            Description = "Test availability", Address = "Sydney", ContactEmail = "slots@example.test", TimeZoneId = zone };
        var service = new Service { BusinessId = business.Id, Name = "Cut", Description = "Test cut", Price = 35m, Currency = "AUD", DurationMinutes = duration };
        context.AddRange(owner, customer, business, service); await context.SaveChangesAsync();
        return (business, service, customer.Id);
    }
    private static async Task HoursAsync(ApplicationDbContext context, Business business, DayOfWeek day, params (string Start, string End)[] intervals)
    {
        context.BusinessWorkingHours.AddRange(intervals.Select(i => new BusinessWorkingHours { BusinessId = business.Id,
            DayOfWeek = day, StartTime = TimeOnly.Parse(i.Start), EndTime = TimeOnly.Parse(i.End) }));
        await context.SaveChangesAsync();
    }
    private static Task<AvailabilityResponse> SlotsAsync(ApplicationDbContext context, Business business, Service service, DateOnly? date = null, TimeProvider? clock = null)
        => new AvailabilityService(context, clock ?? BeforeTestDate).GetAsync(business.Id, service.Id, date ?? Monday, CancellationToken.None);

    [PostgresFact]
    public async Task WeeklyHours_ReplaceEntireWeek_WithLunchBreakAndClosedDays()
    {
        await using var factory = await FactoryAsync(); using var owner = await SignedInAsync(factory);
        await ApiBusinessAsync(owner);
        var initial = (await owner.GetFromJsonAsync<WeeklyHoursResponse>("/api/owner/business/working-hours"))!;
        Assert.Equal(7, initial.Days.Count); Assert.All(initial.Days, d => Assert.Empty(d.Intervals));
        var response = await owner.PutAsJsonAsync("/api/owner/business/working-hours", Week((1, "09:00", "12:00"), (1, "13:00", "17:00")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await owner.GetFromJsonAsync<WeeklyHoursResponse>("/api/owner/business/working-hours"))!;
        Assert.Equal(2, saved.Days.Single(d => d.DayOfWeek == 1).Intervals.Count);
        Assert.All(saved.Days.Where(d => d.DayOfWeek != 1), d => Assert.Empty(d.Intervals));
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync("/api/owner/business/working-hours", Week())).StatusCode);
        Assert.All((await owner.GetFromJsonAsync<WeeklyHoursResponse>("/api/owner/business/working-hours"))!.Days, d => Assert.Empty(d.Intervals));
    }

    [PostgresTheory]
    [InlineData("overlap")]
    [InlineData("equal")]
    [InlineData("overnight")]
    [InlineData("duplicate-day")]
    [InlineData("missing-day")]
    [InlineData("seconds")]
    [InlineData("null-day")]
    public async Task InvalidWeek_IsRejectedWithoutReplacingExistingHours(string invalid)
    {
        await using var factory = await FactoryAsync(); using var owner = await SignedInAsync(factory);
        await ApiBusinessAsync(owner);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync("/api/owner/business/working-hours", Week((1, "09:00", "12:00")))).StatusCode);
        var week = invalid switch
        {
            "overlap" => Week((1, "09:00", "12:00"), (1, "11:00", "14:00")),
            "equal" => Week((1, "09:00", "09:00")),
            "overnight" => Week((1, "22:00", "02:00")),
            "seconds" => Week((1, "09:00:30", "12:00")),
            _ => Week()
        };
        if (invalid == "duplicate-day") week.Days[1] = week.Days[0];
        if (invalid == "missing-day") week.Days.RemoveAt(0);
        if (invalid == "null-day") week.Days[0] = null!;
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/owner/business/working-hours", week)).StatusCode);
        var preserved = (await owner.GetFromJsonAsync<WeeklyHoursResponse>("/api/owner/business/working-hours"))!;
        Assert.Equal(new TimeOnly(9, 0), Assert.Single(preserved.Days.Single(d => d.DayOfWeek == 1).Intervals).StartTime);
    }

    [PostgresTheory]
    [InlineData("invalid-time")]
    [InlineData("missing-start")]
    [InlineData("null-interval")]
    [InlineData("too-many")]
    public async Task MalformedWorkingIntervals_ReturnValidationErrors(string invalid)
    {
        await using var factory = await FactoryAsync(); using var owner = await SignedInAsync(factory); await ApiBusinessAsync(owner);
        var body = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(Week((1, "09:00", "12:00")),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;
        var intervals = body["days"]![0]!["intervals"]!.AsArray();
        if (invalid == "invalid-time") intervals[0]!["startTime"] = "25:00";
        if (invalid == "missing-start") intervals[0]!.AsObject().Remove("startTime");
        if (invalid == "null-interval") intervals[0] = null;
        if (invalid == "too-many") for (var n = 0; n < 8; n++) intervals.Add(intervals[0]!.DeepClone());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/owner/business/working-hours", body)).StatusCode);
    }

    [PostgresFact]
    public async Task AvailabilityManagement_RequiresOwnerRoleAndAuthentication()
    {
        await using var factory = await FactoryAsync(); using var customer = await SignedInAsync(factory, "Customer");
        using var publicClient = factory.CreateClient();
        foreach (var (client, expected) in new[] { (customer, HttpStatusCode.Forbidden), (publicClient, HttpStatusCode.Unauthorized) })
        {
            Assert.Equal(expected, (await client.GetAsync("/api/owner/business/working-hours")).StatusCode);
            Assert.Equal(expected, (await client.PutAsJsonAsync("/api/owner/business/working-hours", Week())).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/owner/business/blocked-periods")).StatusCode);
            Assert.Equal(expected, (await client.PostAsJsonAsync("/api/owner/business/blocked-periods", new BlockedPeriodRequest("2030-01-01T00:00:00Z", "2030-01-02T00:00:00Z", null))).StatusCode);
            Assert.Equal(expected, (await client.DeleteAsync($"/api/owner/business/blocked-periods/{Guid.NewGuid()}")).StatusCode);
        }
    }

    [PostgresFact]
    public async Task OtherOwnerCannotTargetAnotherBusiness_WithForgedBodyBusinessId()
    {
        await using var factory = await FactoryAsync(); using var owner = await SignedInAsync(factory);
        var first = await ApiBusinessAsync(owner);
        await owner.PutAsJsonAsync("/api/owner/business/working-hours", Week((1, "09:00", "12:00")));
        using var other = await SignedInAsync(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync("/api/owner/business/working-hours", Week())).StatusCode);
        var second = await ApiBusinessAsync(other);
        var response = await other.PutAsJsonAsync("/api/owner/business/working-hours", new { BusinessId = first.Id, Days = Week((1, "13:00", "17:00")).Days });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var original = (await owner.GetFromJsonAsync<WeeklyHoursResponse>("/api/owner/business/working-hours"))!;
        Assert.Equal(new TimeOnly(9, 0), Assert.Single(original.Days.Single(d => d.DayOfWeek == 1).Intervals).StartTime);
        await using var context = database.CreateContext();
        Assert.Equal(new TimeOnly(13, 0), (await context.BusinessWorkingHours.SingleAsync(h => h.BusinessId == second.Id)).StartTime);
    }

    [PostgresFact]
    public async Task Closures_CreateListDelete_AndConcealAnotherOwnersPeriod()
    {
        await using var factory = await FactoryAsync(); using var owner = await SignedInAsync(factory);
        await ApiBusinessAsync(owner);
        var response = await owner.PostAsJsonAsync("/api/owner/business/blocked-periods", new BlockedPeriodRequest("2030-01-07T00:00:00Z", "2030-01-07T01:00:00Z", " Maintenance "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var closure = (await response.Content.ReadFromJsonAsync<BlockedPeriodResponse>())!;
        Assert.Equal("Maintenance", closure.Reason); Assert.Equal(DateTimeKind.Utc, closure.StartUtc.Kind);
        Assert.Contains((await owner.GetFromJsonAsync<BlockedPeriodResponse[]>("/api/owner/business/blocked-periods"))!, p => p.Id == closure.Id);
        using var other = await SignedInAsync(factory); await ApiBusinessAsync(other);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/owner/business/blocked-periods/{closure.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/owner/business/blocked-periods/{closure.Id}")).StatusCode);
        Assert.DoesNotContain((await owner.GetFromJsonAsync<BlockedPeriodResponse[]>("/api/owner/business/blocked-periods"))!, p => p.Id == closure.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/api/owner/business/blocked-periods/{closure.Id}")).StatusCode);
    }

    [PostgresFact]
    public async Task CustomClosureTimes_RoundTripAndExcludeOnlyOverlappingSlots()
    {
        await using var factory = await FactoryAsync();
        using var owner = await SignedInAsync(factory);
        var business = await ApiBusinessAsync(owner);
        await using var context = database.CreateContext();
        var entity = await context.Businesses.SingleAsync(b => b.Id == business.Id);
        var service = new Service { BusinessId = business.Id, Name = "Custom closure test", Description = "Test",
            Price = 35m, Currency = "AUD", DurationMinutes = 30 };
        context.Services.Add(service);
        await HoursAsync(context, entity, Monday.DayOfWeek, ("09:00", "17:00"));

        // Sydney daylight time: 01:25–03:40 UTC is 12:25–14:40 business-local.
        var start = "2030-01-07T01:25:00.000Z";
        var end = "2030-01-07T03:40:00.000Z";
        var response = await owner.PostAsJsonAsync("/api/owner/business/blocked-periods", new BlockedPeriodRequest(start, end, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<BlockedPeriodResponse>())!;
        Assert.Equal(new DateTime(2030, 1, 7, 1, 25, 0, DateTimeKind.Utc), saved.StartUtc);
        Assert.Equal(new DateTime(2030, 1, 7, 3, 40, 0, DateTimeKind.Utc), saved.EndUtc);
        Assert.Null(saved.Reason);
        using var visitor = factory.CreateClient();
        var slots = (await visitor.GetFromJsonAsync<AvailabilityResponse>($"/api/businesses/{business.Id}/availability?serviceId={service.Id}&date=2030-01-07"))!;
        Assert.NotEmpty(slots.Slots);
        Assert.DoesNotContain(slots.Slots, slot => slot.StartUtc < saved.EndUtc && slot.EndUtc > saved.StartUtc);
        Assert.Contains(slots.Slots, slot => slot.StartUtc == Utc(Monday, "11:45"));
        Assert.DoesNotContain(slots.Slots, slot => slot.StartUtc == Utc(Monday, "12:00"));
        Assert.Contains(slots.Slots, slot => slot.StartUtc == Utc(Monday, "14:45"));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/owner/business/blocked-periods/{saved.Id}")).StatusCode);
        var restored = (await visitor.GetFromJsonAsync<AvailabilityResponse>($"/api/businesses/{business.Id}/availability?serviceId={service.Id}&date=2030-01-07"))!;
        Assert.Contains(restored.Slots, slot => slot.StartUtc == Utc(Monday, "12:00"));
    }

    [PostgresTheory]
    [InlineData("2030-01-07T00:00:00Z", "2030-01-07T00:00:00Z")]
    [InlineData("2030-01-07T01:00:00Z", "2030-01-07T00:00:00Z")]
    [InlineData("2030-01-07T00:00:00", "2030-01-07T01:00:00Z")]
    [InlineData("2030-01-07T00:00:00+10:00", "2030-01-07T01:00:00Z")]
    [InlineData("2030-01-07T00:00:00Z", "2032-01-07T00:00:00Z")]
    [InlineData("1900-01-07T00:00:00Z", "1900-01-07T01:00:00Z")]
    public async Task InvalidClosure_IsRejected(string start, string end)
    {
        await using var factory = await FactoryAsync(); using var owner = await SignedInAsync(factory); await ApiBusinessAsync(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/owner/business/blocked-periods", new BlockedPeriodRequest(start, end, null))).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<BlockedPeriodResponse[]>("/api/owner/business/blocked-periods"))!);
    }

    [PostgresTheory]
    [InlineData(30, 11, "11:30")]
    [InlineData(60, 9, "11:00")]
    [InlineData(180, 1, "09:00")]
    [InlineData(181, 0, "")]
    public async Task DurationDeterminesFinalStart_AndSlotsUseQuarterHours(int duration, int count, string last)
    {
        await using var context = database.CreateContext(); var (business, service, _) = await GraphAsync(context, duration);
        await HoursAsync(context, business, Monday.DayOfWeek, ("09:00", "12:00"));
        var result = await SlotsAsync(context, business, service);
        Assert.Equal(count, result.Slots.Count);
        if (count > 0) Assert.Equal(Utc(Monday, last), result.Slots[^1].StartUtc);
        Assert.All(result.Slots, s => { Assert.True(s.EndUtc <= Utc(Monday, "12:00")); Assert.Equal(TimeSpan.FromMinutes(duration), s.EndUtc - s.StartUtc); });
        for (var i = 1; i < result.Slots.Count; i++) Assert.Equal(TimeSpan.FromMinutes(15), result.Slots[i].StartUtc - result.Slots[i - 1].StartUtc);
    }

    [PostgresFact]
    public async Task LunchBreak_RequiresAnAppointmentToFitInsideOneInterval()
    {
        await using var context = database.CreateContext(); var (business, service, _) = await GraphAsync(context);
        await HoursAsync(context, business, Monday.DayOfWeek, ("09:00", "12:00"), ("13:00", "17:00"));
        var result = await SlotsAsync(context, business, service); Assert.Equal(26, result.Slots.Count);
        Assert.All(result.Slots, s => Assert.True(s.EndUtc <= Utc(Monday, "12:00") || s.StartUtc >= Utc(Monday, "13:00")));
        Assert.Contains(result.Slots, s => s.StartUtc == Utc(Monday, "11:30"));
        Assert.DoesNotContain(result.Slots, s => s.StartUtc == Utc(Monday, "11:45"));
    }

    [PostgresFact]
    public async Task ClosedDay_ReturnsNoSlots_AndOffGridOpeningRoundsUp()
    {
        await using var context = database.CreateContext(); var (business, service, _) = await GraphAsync(context);
        Assert.Empty((await SlotsAsync(context, business, service)).Slots);
        await HoursAsync(context, business, Monday.DayOfWeek, ("09:10", "12:00"));
        Assert.Equal(Utc(Monday, "09:15"), (await SlotsAsync(context, business, service)).Slots[0].StartUtc);
    }

    [PostgresFact]
    public async Task PastTimes_AreExcludedUsingInjectedClock()
    {
        await using var context = database.CreateContext(); var (business, service, _) = await GraphAsync(context);
        await HoursAsync(context, business, Monday.DayOfWeek, ("09:00", "12:00"));
        var clock = new FixedClock(Utc(Monday, "09:30"));
        var result = await SlotsAsync(context, business, service, clock: clock);
        Assert.Equal(Utc(Monday, "09:45"), result.Slots[0].StartUtc);
        Assert.Empty((await SlotsAsync(context, business, service, Monday.AddDays(-7), clock)).Slots);
    }

    [PostgresFact]
    public async Task ClosureOverlap_UsesHalfOpenIntervals_AndIncludesCrossDayClosures()
    {
        await using var context = database.CreateContext(); var (business, service, _) = await GraphAsync(context);
        await HoursAsync(context, business, Monday.DayOfWeek, ("09:00", "12:00"));
        context.BusinessBlockedPeriods.Add(new BusinessBlockedPeriod { BusinessId = business.Id,
            StartUtc = Utc(Monday, "09:30"), EndUtc = Utc(Monday, "10:00") }); await context.SaveChangesAsync();
        var result = await SlotsAsync(context, business, service); Assert.Equal(8, result.Slots.Count);
        Assert.Contains(result.Slots, s => s.StartUtc == Utc(Monday, "09:00")); // ends exactly at closure start
        Assert.Contains(result.Slots, s => s.StartUtc == Utc(Monday, "10:00")); // starts exactly at closure end
        Assert.DoesNotContain(result.Slots, s => s.StartUtc == Utc(Monday, "09:15"));
        context.BusinessBlockedPeriods.Add(new BusinessBlockedPeriod { BusinessId = business.Id,
            StartUtc = Utc(Monday.AddDays(-1), "23:00"), EndUtc = Utc(Monday, "09:15") }); await context.SaveChangesAsync();
        Assert.DoesNotContain((await SlotsAsync(context, business, service)).Slots, s => s.StartUtc < Utc(Monday, "10:00"));
    }

    [PostgresTheory]
    [InlineData(BookingStatus.Confirmed, 8)]
    [InlineData(BookingStatus.Cancelled, 11)]
    [InlineData(BookingStatus.Completed, 11)]
    public async Task ExistingBookings_BlockOnlyWhenConfirmed_AcrossAllBusinessServices(BookingStatus status, int expected)
    {
        await using var context = database.CreateContext(); var (business, service, customerId) = await GraphAsync(context);
        await HoursAsync(context, business, Monday.DayOfWeek, ("09:00", "12:00"));
        var otherService = new Service { BusinessId = business.Id, Name = "Other", Description = "Other service", Price = 40, Currency = "AUD", DurationMinutes = 30 };
        context.Services.Add(otherService);
        context.Bookings.Add(new Booking { BusinessId = business.Id, CustomerId = customerId, ServiceId = otherService.Id,
            StartUtc = Utc(Monday, "09:30"), EndUtc = Utc(Monday, "10:00"), Status = status,
            ServiceName = otherService.Name, ServicePrice = otherService.Price, ServiceCurrency = "AUD", ServiceDurationMinutes = 30 });
        await context.SaveChangesAsync();
        var result = await SlotsAsync(context, business, service); Assert.Equal(expected, result.Slots.Count);
        Assert.Contains(result.Slots, s => s.StartUtc == Utc(Monday, "09:00"));
        Assert.Contains(result.Slots, s => s.StartUtc == Utc(Monday, "10:00"));
    }

    [PostgresTheory]
    [InlineData("business")]
    [InlineData("service")]
    [InlineData("owner")]
    [InlineData("cross-business")]
    public async Task UnavailableBusinessServicePair_IsRejected(string invalid)
    {
        await using var context = database.CreateContext(); var (business, service, _) = await GraphAsync(context);
        if (invalid == "business") business.IsActive = false;
        if (invalid == "service") service.IsActive = false;
        if (invalid == "owner") (await context.Users.SingleAsync(u => u.Id == business.OwnerId)).IsActive = false;
        if (invalid == "cross-business") { var other = await GraphAsync(context); service = other.Service; }
        await context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<RequestException>(() => SlotsAsync(context, business, service)); Assert.Equal(404, error.StatusCode);
    }

    [PostgresTheory]
    [InlineData("Australia/Sydney", "2030-01-06T22:00:00Z")]
    [InlineData("Australia/Brisbane", "2030-01-06T23:00:00Z")]
    public async Task BusinessLocalDate_ConvertsToCorrectUtc(string zone, string expected)
    {
        await using var context = database.CreateContext(); var (business, service, _) = await GraphAsync(context, zone: zone);
        await HoursAsync(context, business, Monday.DayOfWeek, ("09:00", "10:00"));
        Assert.Equal(DateTime.Parse(expected).ToUniversalTime(), (await SlotsAsync(context, business, service)).Slots[0].StartUtc);
    }

    [PostgresTheory]
    [InlineData(2030, 10, 6)] // Sydney spring-forward: 02:00–02:59 does not exist.
    [InlineData(2030, 4, 7)] // Sydney fall-back: 02:00–02:59 occurs twice.
    public async Task DstTransitions_ExcludeInvalidAmbiguousEndpointsAndCrossingAppointments(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        await using var context = database.CreateContext(); var (business, service, _) = await GraphAsync(context);
        await HoursAsync(context, business, date.DayOfWeek, ("01:00", "04:00"));
        var result = await SlotsAsync(context, business, service, date); Assert.Equal(5, result.Slots.Count);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(business.TimeZoneId);
        Assert.All(result.Slots, slot =>
        {
            var start = TimeZoneInfo.ConvertTimeFromUtc(slot.StartUtc, zone);
            var end = TimeZoneInfo.ConvertTimeFromUtc(slot.EndUtc, zone);
            Assert.False(zone.IsInvalidTime(start) || zone.IsAmbiguousTime(start) || zone.IsInvalidTime(end) || zone.IsAmbiguousTime(end));
            Assert.Equal(TimeSpan.FromMinutes(30), slot.EndUtc - slot.StartUtc);
            Assert.NotEqual(2, start.Hour);
        });
        // Valid endpoints can still span the transition; a two-hour wall appointment cannot cross it.
        service.DurationMinutes = 120;
        var hours = await context.BusinessWorkingHours.SingleAsync(h => h.BusinessId == business.Id);
        hours.EndTime = new TimeOnly(5, 0); await context.SaveChangesAsync();
        var longSlots = await SlotsAsync(context, business, service, date);
        Assert.Equal(Utc(date, "03:00"), Assert.Single(longSlots.Slots).StartUtc);
    }

    [PostgresFact]
    public async Task PublicEndpoint_ReturnsUtcSlotsAndValidatesQuery()
    {
        await using var factory = await FactoryAsync(); using var owner = await SignedInAsync(factory);
        var business = await ApiBusinessAsync(owner);
        var created = await owner.PostAsJsonAsync($"/api/businesses/{business.Id}/services", new ServiceRequest("Cut", "Consultation and cut", 35m, "AUD", 30));
        var service = (await created.Content.ReadFromJsonAsync<ServiceResponse>())!;
        await owner.PutAsJsonAsync("/api/owner/business/working-hours", Week((1, "09:00", "12:00")));
        using var publicClient = factory.CreateClient();
        var url = $"/api/businesses/{business.Id}/availability";
        var result = (await publicClient.GetFromJsonAsync<AvailabilityResponse>($"{url}?serviceId={service.Id}&date=2030-01-07"))!;
        Assert.Equal(11, result.Slots.Count); Assert.Equal("Australia/Sydney", result.TimeZoneId);
        Assert.Equal(DateTimeKind.Utc, result.Slots[0].StartUtc.Kind);
        Assert.Equal(HttpStatusCode.BadRequest, (await publicClient.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await publicClient.GetAsync($"{url}?serviceId={service.Id}&date=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync($"{url}?serviceId={Guid.NewGuid()}&date=2030-01-07")).StatusCode);
    }
}
