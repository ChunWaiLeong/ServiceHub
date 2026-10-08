using Microsoft.EntityFrameworkCore;
using ServiceHub.Api.Contracts.Availability;
using ServiceHub.Api.Data;
using ServiceHub.Api.ErrorHandling;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Services;

public sealed class AvailabilityService(ApplicationDbContext context, TimeProvider clock)
{
    public async Task<AvailabilityResponse> GetAsync(Guid businessId, Guid serviceId, DateOnly date, CancellationToken ct)
    {
        if (date.Year < 2000 || date.Year > 2100)
            throw new RequestException(400, "Choose a date between years 2000 and 2100.");
        var service = await context.Services.AsNoTracking().Where(s => s.Id == serviceId && s.BusinessId == businessId
            && s.IsActive && s.Business.IsActive && s.Business.Owner.IsActive)
            .Select(s => new { s.DurationMinutes, s.Business.TimeZoneId }).SingleOrDefaultAsync(ct)
            ?? throw new RequestException(404, "Business or service not available.");
        var zone = ResolveTimeZone(service.TimeZoneId);
        var now = clock.GetUtcNow().UtcDateTime;
        var slots = new List<AvailableSlotResponse>();
        var response = new AvailabilityResponse(businessId, serviceId, date, service.TimeZoneId, service.DurationMinutes, slots);
        if (date < DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone))) return response;

        var hours = await context.BusinessWorkingHours.AsNoTracking()
            .Where(h => h.BusinessId == businessId && h.DayOfWeek == date.DayOfWeek)
            .OrderBy(h => h.StartTime).ToListAsync(ct);
        foreach (var interval in hours)
        {
            var opening = date.ToDateTime(interval.StartTime, DateTimeKind.Unspecified);
            var closing = date.ToDateTime(interval.EndTime, DateTimeKind.Unspecified);
            // Align to quarter-hour clock boundaries, even when a business opens at 09:10.
            var first = opening.AddMinutes((15 - opening.Minute % 15) % 15);
            for (var localStart = first; localStart.AddMinutes(service.DurationMinutes) <= closing; localStart = localStart.AddMinutes(15))
            {
                ct.ThrowIfCancellationRequested();
                var localEnd = localStart.AddMinutes(service.DurationMinutes);
                if (zone.IsInvalidTime(localStart) || zone.IsInvalidTime(localEnd)
                    || zone.IsAmbiguousTime(localStart) || zone.IsAmbiguousTime(localEnd)) continue;
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
                // Reject appointments crossing a DST offset change, including valid endpoints on either side.
                if (endUtc - startUtc != TimeSpan.FromMinutes(service.DurationMinutes) || startUtc <= now) continue;
                slots.Add(new(startUtc, endUtc));
            }
        }
        if (slots.Count == 0) return response;
        var rangeStart = slots.Min(s => s.StartUtc);
        var rangeEnd = slots.Max(s => s.EndUtc);
        var closures = await context.BusinessBlockedPeriods.AsNoTracking()
            .Where(p => p.BusinessId == businessId && p.StartUtc < rangeEnd && p.EndUtc > rangeStart)
            .Select(p => new { p.StartUtc, p.EndUtc }).ToListAsync(ct);
        var bookings = await context.Bookings.AsNoTracking()
            .Where(b => b.BusinessId == businessId && b.Status == BookingStatus.Confirmed
                && b.StartUtc < rangeEnd && b.EndUtc > rangeStart)
            .Select(b => new { b.StartUtc, b.EndUtc }).ToListAsync(ct);
        slots.RemoveAll(slot => closures.Any(p => slot.StartUtc < p.EndUtc && slot.EndUtc > p.StartUtc)
            || bookings.Any(b => slot.StartUtc < b.EndUtc && slot.EndUtc > b.StartUtc));
        return response with { Slots = slots.Distinct().OrderBy(s => s.StartUtc).ToList() };
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { throw new RequestException(400, "The business time zone is unavailable on this server."); }
        catch (InvalidTimeZoneException) { throw new RequestException(400, "The business time zone is invalid."); }
    }
}
