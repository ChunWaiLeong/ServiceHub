using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceHub.Api.Contracts.Bookings;
using ServiceHub.Api.Data;
using ServiceHub.Api.ErrorHandling;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Services;

public sealed class BookingService(ApplicationDbContext context, AvailabilityService availability, BusinessService businesses, TimeProvider clock)
{
    public const string OverlapConstraint = "EX_Bookings_ConfirmedBusinessOverlap";

    public async Task<BookingResponse> CreateAsync(Guid customerId, CreateBookingRequest request, CancellationToken ct)
    {
        await EnsureActiveAsync(customerId, ct);
        var start = ParseStart(request.StartUtc);
        if (start <= clock.GetUtcNow().UtcDateTime) throw new RequestException(400, "Choose a future appointment time.");
        var service = await context.Services.AsNoTracking().Include(s => s.Business)
            .SingleOrDefaultAsync(s => s.Id == request.ServiceId && s.IsActive && s.Business.IsActive && s.Business.Owner.IsActive, ct)
            ?? throw new RequestException(404, "Business or service not available.");
        var zone = AvailabilityService.ResolveTimeZone(service.Business.TimeZoneId);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(start, zone));
        // Use the same slot rules as the public preview; previously displayed slots reserve nothing.
        var current = await availability.GetAsync(service.BusinessId, service.Id, date, ct);
        var end = start.AddMinutes(service.DurationMinutes);
        if (!current.Slots.Any(slot => slot.StartUtc == start && slot.EndUtc == end))
        {
            if (await context.Bookings.AnyAsync(b => b.BusinessId == service.BusinessId && b.Status == BookingStatus.Confirmed
                && b.StartUtc < end && b.EndUtc > start, ct))
                throw SlotConflict();
            throw new RequestException(400, "Choose a currently available appointment time.");
        }
        var booking = new Booking { CustomerId = customerId, BusinessId = service.BusinessId, ServiceId = service.Id,
            StartUtc = start, EndUtc = end, Status = BookingStatus.Confirmed, CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            ServiceName = service.Name, ServicePrice = service.Price, ServiceCurrency = service.Currency,
            ServiceDurationMinutes = service.DurationMinutes };
        context.Bookings.Add(booking);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: OverlapConstraint })
        {
            throw SlotConflict();
        }
        return await GetCustomerAsync(customerId, booking.Id, ct);
    }

    public async Task<IReadOnlyList<BookingResponse>> ListCustomerAsync(Guid customerId, CancellationToken ct)
    {
        await EnsureActiveAsync(customerId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        return await Project(context.Bookings.AsNoTracking().Where(b => b.CustomerId == customerId)
            .OrderBy(b => !(b.Status == BookingStatus.Confirmed && b.StartUtc > now))
            .ThenBy(b => b.Status == BookingStatus.Confirmed && b.StartUtc > now ? b.StartUtc : DateTime.MaxValue)
            .ThenByDescending(b => b.StartUtc).ThenBy(b => b.Id), false).ToListAsync(ct);
    }

    public async Task<BookingResponse> GetCustomerAsync(Guid customerId, Guid id, CancellationToken ct)
    {
        await EnsureActiveAsync(customerId, ct);
        return await Project(context.Bookings.AsNoTracking().Where(b => b.Id == id && b.CustomerId == customerId), false)
            .SingleOrDefaultAsync(ct) ?? throw Missing();
    }

    public async Task<IReadOnlyList<BookingResponse>> ListOwnerAsync(Guid ownerId, OwnerBookingQuery query, CancellationToken ct)
    {
        var business = await businesses.GetOwnerAsync(ownerId, ct);
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc >= query.ToUtc)
            throw new RequestException(400, "The date range must end after it starts.");
        var bookings = context.Bookings.AsNoTracking().Where(b => b.BusinessId == business.Id);
        if (query.Status is not null)
        {
            if (!Enum.TryParse<BookingStatus>(query.Status, out var status) || !Enum.IsDefined(status))
                throw new RequestException(400, "Choose a valid booking status.");
            bookings = bookings.Where(b => b.Status == status);
        }
        if (query.FromUtc.HasValue) { var from = query.FromUtc.Value.UtcDateTime; bookings = bookings.Where(b => b.StartUtc >= from); }
        if (query.ToUtc.HasValue) { var to = query.ToUtc.Value.UtcDateTime; bookings = bookings.Where(b => b.StartUtc < to); }
        var now = clock.GetUtcNow().UtcDateTime;
        return await Project(bookings.OrderBy(b => !(b.Status == BookingStatus.Confirmed && b.StartUtc > now))
            .ThenBy(b => b.Status == BookingStatus.Confirmed && b.StartUtc > now ? b.StartUtc : DateTime.MaxValue)
            .ThenByDescending(b => b.StartUtc).ThenBy(b => b.Id), true).ToListAsync(ct);
    }

    public async Task<BookingResponse> GetOwnerAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        var business = await businesses.GetOwnerAsync(ownerId, ct);
        return await Project(context.Bookings.AsNoTracking().Where(b => b.Id == id && b.BusinessId == business.Id), true)
            .SingleOrDefaultAsync(ct) ?? throw Missing();
    }

    public async Task<BookingResponse> CancelCustomerAsync(Guid customerId, Guid id, CancellationToken ct)
    {
        await GetCustomerAsync(customerId, id, ct);
        await TransitionAsync(id, BookingStatus.Cancelled, true, ct);
        return await GetCustomerAsync(customerId, id, ct);
    }
    public async Task<BookingResponse> CancelOwnerAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        await GetOwnerAsync(ownerId, id, ct);
        await TransitionAsync(id, BookingStatus.Cancelled, false, ct);
        return await GetOwnerAsync(ownerId, id, ct);
    }
    public async Task<BookingResponse> CompleteOwnerAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        await GetOwnerAsync(ownerId, id, ct);
        await TransitionAsync(id, BookingStatus.Completed, false, ct);
        return await GetOwnerAsync(ownerId, id, ct);
    }
    private async Task TransitionAsync(Guid id, BookingStatus target, bool customerCancellation, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var eligible = context.Bookings.Where(b => b.Id == id && b.Status == BookingStatus.Confirmed);
        if (customerCancellation) eligible = eligible.Where(b => b.StartUtc > now);
        if (target == BookingStatus.Completed) eligible = eligible.Where(b => b.EndUtc <= now);
        // Conditional SQL update prevents two simultaneous transitions overwriting each other.
        var count = await eligible.ExecuteUpdateAsync(update => update.SetProperty(b => b.Status, target)
            .SetProperty(b => b.CancelledAtUtc, target == BookingStatus.Cancelled ? (DateTime?)now : null), ct);
        if (count == 0) throw new RequestException(400, target == BookingStatus.Completed
            ? "Only a Confirmed appointment that has ended can be completed."
            : customerCancellation ? "Only a future Confirmed appointment can be cancelled." : "Only a Confirmed appointment can be cancelled.");
    }
    private async Task EnsureActiveAsync(Guid userId, CancellationToken ct)
    {
        if (!await context.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct))
            throw new RequestException(401, "Your account is unavailable. Please sign in again.");
    }
    private static IQueryable<BookingResponse> Project(IQueryable<Booking> query, bool owner)
        => query.Select(b => new BookingResponse(b.Id, b.BusinessId, b.Business.Name, b.Business.Address, b.Business.TimeZoneId,
            b.ServiceId, b.ServiceName, b.ServicePrice, b.ServiceCurrency, b.ServiceDurationMinutes, b.StartUtc, b.EndUtc,
            b.Status.ToString(), b.CreatedAtUtc, b.CancelledAtUtc, owner ? new BookingCustomerResponse(
                b.Customer.FirstName + " " + b.Customer.LastName, b.Customer.Email!) : null));
    private static RequestException Missing() => new(404, "Booking not found.");
    private static RequestException SlotConflict() => new(409, "That time is no longer available. Please choose another appointment time.");
    private static DateTime ParseStart(string value)
    {
        if ((!value.EndsWith("Z", StringComparison.Ordinal) && !value.EndsWith("+00:00", StringComparison.Ordinal))
            || !value.Contains('T') || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || parsed.Offset != TimeSpan.Zero || parsed.Year < 2000 || parsed.Year > 2100)
            throw new RequestException(400, "Use an ISO-8601 UTC appointment start ending in Z or +00:00, between years 2000 and 2100.");
        return parsed.UtcDateTime;
    }
}
