using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceHub.Api.Data;
using ServiceHub.Api.Data.Seeding;
using ServiceHub.Api.Models;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SERVICEHUB_TEST_DATABASE")))
            Skip = "Set SERVICEHUB_TEST_DATABASE to a dedicated PostgreSQL test database.";
    }
}

public sealed class PostgresPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static Business NewBusiness(Guid ownerId, Guid categoryId) => new()
    {
        OwnerId = ownerId, BusinessCategoryId = categoryId, Name = "Test business",
        Description = "Persistence test", Address = "1 Test Street", ContactEmail = "test@example.test",
        TimeZoneId = "Australia/Sydney"
    };

    private static async Task<Booking> SeedGraphAsync(ApplicationDbContext context)
    {
        var owner = new ApplicationUser { FirstName = "Owner", LastName = "Test" };
        var customer = new ApplicationUser { FirstName = "Customer", LastName = "Test" };
        var category = new BusinessCategory { Name = "Test " + Guid.NewGuid().ToString("N") };
        var business = NewBusiness(owner.Id, category.Id);
        var service = new Service
        {
            BusinessId = business.Id, Name = "Consultation", Description = "Test service",
            Price = 49.95m, Currency = "AUD", DurationMinutes = 30
        };
        var start = new DateTime(2030, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var booking = new Booking
        {
            CustomerId = customer.Id, BusinessId = business.Id, ServiceId = service.Id,
            StartUtc = start, EndUtc = start.AddMinutes(30), ServiceName = service.Name,
            ServicePrice = service.Price, ServiceCurrency = service.Currency,
            ServiceDurationMinutes = service.DurationMinutes
        };
        context.AddRange(owner, customer, category, business, service, booking);
        await context.SaveChangesAsync();
        return booking;
    }

    private static void AssertPostgresError(DbUpdateException exception, string sqlState) =>
        Assert.Equal(sqlState, Assert.IsType<PostgresException>(exception.InnerException).SqlState);

    [PostgresFact]
    public async Task Relationships_RoundTripThroughPostgres()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var booking = await SeedGraphAsync(context);
        context.ChangeTracker.Clear();
        var saved = await context.Bookings.Include(item => item.Customer)
            .Include(item => item.Business).ThenInclude(business => business.BusinessCategory)
            .Include(item => item.Business).ThenInclude(business => business.Owner)
            .Include(item => item.Service).SingleAsync(item => item.Id == booking.Id);
        Assert.Equal("Customer", saved.Customer.FirstName);
        Assert.Equal("Owner", saved.Business.Owner.FirstName);
        Assert.StartsWith("Test ", saved.Business.BusinessCategory.Name);
        Assert.Equal(saved.BusinessId, saved.Service.BusinessId);
    }

    [PostgresFact]
    public async Task OneBusinessPerOwner_IsEnforced()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var booking = await SeedGraphAsync(context);
        var business = await context.Businesses.SingleAsync(item => item.Id == booking.BusinessId);
        // Detach the seeded graph so one-to-one fix-up cannot replace the owner's first business.
        context.ChangeTracker.Clear();
        context.Businesses.Add(NewBusiness(business.OwnerId, business.BusinessCategoryId));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("IX_Businesses_OwnerId", postgresException.ConstraintName);
    }

    [PostgresFact]
    public async Task CategoryNames_AreUnique()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var name = "Unique " + Guid.NewGuid().ToString("N");
        context.BusinessCategories.Add(new BusinessCategory { Name = name });
        await context.SaveChangesAsync();
        context.BusinessCategories.Add(new BusinessCategory { Name = name });
        AssertPostgresError(await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()), PostgresErrorCodes.UniqueViolation);
    }

    [PostgresFact]
    public async Task Price_UsesTwoDecimalPlaces()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var booking = await SeedGraphAsync(context);
        var service = await context.Services.SingleAsync(item => item.Id == booking.ServiceId);
        service.Price = 49.995m;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(50.00m, (await context.Services.SingleAsync(item => item.Id == booking.ServiceId)).Price);
    }

    [PostgresFact]
    public async Task WorkingHours_SupportTwoIntervalsOnOneDay()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var booking = await SeedGraphAsync(context);
        context.BusinessWorkingHours.AddRange(
            new BusinessWorkingHours { BusinessId = booking.BusinessId, DayOfWeek = DayOfWeek.Monday, StartTime = new(9, 0), EndTime = new(12, 0) },
            new BusinessWorkingHours { BusinessId = booking.BusinessId, DayOfWeek = DayOfWeek.Monday, StartTime = new(13, 0), EndTime = new(17, 0) });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var intervals = await context.BusinessWorkingHours.Where(item => item.BusinessId == booking.BusinessId).OrderBy(item => item.StartTime).ToListAsync();
        Assert.Equal(2, intervals.Count);
        Assert.Equal(new TimeOnly(13, 0), intervals[1].StartTime);
    }

    [PostgresFact]
    public async Task BlockedPeriod_PreservesUtcTimestampsAndReason()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var booking = await SeedGraphAsync(context);
        var period = new BusinessBlockedPeriod { BusinessId = booking.BusinessId, StartUtc = booking.StartUtc, EndUtc = booking.EndUtc, Reason = "Holiday" };
        context.BusinessBlockedPeriods.Add(period);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var saved = await context.BusinessBlockedPeriods.SingleAsync(item => item.Id == period.Id);
        Assert.Equal(period.StartUtc, saved.StartUtc);
        Assert.Equal(DateTimeKind.Utc, saved.StartUtc.Kind);
        Assert.Equal(period.EndUtc, saved.EndUtc);
        Assert.Equal("Holiday", saved.Reason);
    }

    [PostgresFact]
    public async Task BookingSnapshot_SurvivesServiceEdits()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var booking = await SeedGraphAsync(context);
        var service = await context.Services.SingleAsync(item => item.Id == booking.ServiceId);
        service.Name = "Changed"; service.Price = 90m; service.Currency = "USD"; service.DurationMinutes = 60;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var saved = await context.Bookings.SingleAsync(item => item.Id == booking.Id);
        Assert.Equal("Consultation", saved.ServiceName);
        Assert.Equal(49.95m, saved.ServicePrice);
        Assert.Equal("AUD", saved.ServiceCurrency);
        Assert.Equal(30, saved.ServiceDurationMinutes);
        Assert.Equal(BookingStatus.Confirmed, saved.Status);
    }

    [PostgresFact]
    public async Task Booking_CannotReferenceAnotherBusinessService()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var first = await SeedGraphAsync(context);
        var second = await SeedGraphAsync(context);
        context.ChangeTracker.Clear();
        context.Bookings.Add(new Booking
        {
            BusinessId = first.BusinessId, ServiceId = second.ServiceId, CustomerId = first.CustomerId,
            StartUtc = first.EndUtc, EndUtc = first.EndUtc.AddMinutes(30), ServiceName = "Consultation",
            ServicePrice = 49.95m, ServiceCurrency = "AUD", ServiceDurationMinutes = 30
        });
        AssertPostgresError(await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()), PostgresErrorCodes.ForeignKeyViolation);
    }

    [PostgresTheory]
    [InlineData("service")]
    [InlineData("business")]
    [InlineData("customer")]
    [InlineData("owner")]
    [InlineData("category")]
    public async Task DeleteReferencedData_IsRejectedAndPreservesBooking(string target)
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var booking = await SeedGraphAsync(context);
        var business = await context.Businesses.SingleAsync(item => item.Id == booking.BusinessId);
        Func<Task<int>> delete = target switch
        {
            "service" => () => context.Services.Where(item => item.Id == booking.ServiceId).ExecuteDeleteAsync(),
            "business" => () => context.Businesses.Where(item => item.Id == booking.BusinessId).ExecuteDeleteAsync(),
            "customer" => () => context.Users.Where(item => item.Id == booking.CustomerId).ExecuteDeleteAsync(),
            "owner" => () => context.Users.Where(item => item.Id == business.OwnerId).ExecuteDeleteAsync(),
            _ => () => context.BusinessCategories.Where(item => item.Id == business.BusinessCategoryId).ExecuteDeleteAsync()
        };
        // PostgreSQL aborts a transaction after a failed statement; roll back to a savepoint before reading.
        await transaction.CreateSavepointAsync("before_delete");
        var exception = await Assert.ThrowsAsync<PostgresException>(delete);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        await transaction.RollbackToSavepointAsync("before_delete");
        Assert.True(await context.Bookings.AnyAsync(item => item.Id == booking.Id));
    }

    [PostgresTheory]
    [InlineData("price")]
    [InlineData("duration")]
    [InlineData("working-hours")]
    [InlineData("blocked-period")]
    [InlineData("booking")]
    public async Task InvalidValues_AreRejectedByDatabaseChecks(string target)
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var booking = await SeedGraphAsync(context);
        if (target == "price" || target == "duration")
        {
            var service = await context.Services.SingleAsync(item => item.Id == booking.ServiceId);
            if (target == "price") service.Price = 0;
            else service.DurationMinutes = 0;
        }
        else if (target == "working-hours")
            context.BusinessWorkingHours.Add(new BusinessWorkingHours { BusinessId = booking.BusinessId, DayOfWeek = DayOfWeek.Monday, StartTime = new(17, 0), EndTime = new(9, 0) });
        else if (target == "blocked-period")
            context.BusinessBlockedPeriods.Add(new BusinessBlockedPeriod { BusinessId = booking.BusinessId, StartUtc = booking.StartUtc, EndUtc = booking.StartUtc });
        else booking.EndUtc = booking.StartUtc;
        AssertPostgresError(await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()), PostgresErrorCodes.CheckViolation);
    }

    [PostgresFact]
    public async Task DevelopmentCategories_SeedIdempotently()
    {
        await using var context = fixture.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await DevelopmentDataSeeder.SeedAsync(context, CancellationToken.None);
        await DevelopmentDataSeeder.SeedAsync(context, CancellationToken.None);
        Assert.Equal(5, await context.BusinessCategories.CountAsync());
    }
}
