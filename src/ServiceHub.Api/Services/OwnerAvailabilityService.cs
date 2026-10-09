using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ServiceHub.Api.Contracts.Availability;
using ServiceHub.Api.Data;
using ServiceHub.Api.ErrorHandling;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Services;

public sealed class OwnerAvailabilityService(ApplicationDbContext context, BusinessService businesses, TimeProvider clock)
{
    public async Task<WeeklyHoursResponse> GetHoursAsync(Guid ownerId, CancellationToken ct)
    {
        var business = await businesses.GetOwnerAsync(ownerId, ct);
        var hours = await context.BusinessWorkingHours.AsNoTracking().Where(h => h.BusinessId == business.Id).ToListAsync(ct);
        return MapWeek(hours);
    }

    public async Task<WeeklyHoursResponse> ReplaceHoursAsync(Guid ownerId, WeeklyHoursRequest request, CancellationToken ct)
    {
        var business = await businesses.GetOwnerAsync(ownerId, ct);
        if (request.Days.Any(d => d is null || d.Intervals is null || d.Intervals.Any(i => i is null)))
            throw new RequestException(400, "Days and intervals must not contain null entries.");
        if (request.Days.Count != 7 || request.Days.Select(d => d.DayOfWeek).Distinct().Count() != 7
            || request.Days.Any(d => d.DayOfWeek < 0 || d.DayOfWeek > 6))
            throw new RequestException(400, "Supply each day exactly once, using an empty interval list for closed days.");
        foreach (var day in request.Days)
        {
            var ordered = day.Intervals.OrderBy(i => i.StartTime).ToList();
            for (var index = 0; index < ordered.Count; index++)
            {
                var interval = ordered[index];
                if (!interval.StartTime.HasValue || !interval.EndTime.HasValue || interval.StartTime >= interval.EndTime
                    || interval.StartTime.Value.Ticks % TimeSpan.TicksPerMinute != 0
                    || interval.EndTime.Value.Ticks % TimeSpan.TicksPerMinute != 0)
                    throw new RequestException(400, "Working intervals must use whole minutes and start before they end. Overnight hours are not supported.");
                if (index > 0 && interval.StartTime < ordered[index - 1].EndTime)
                    throw new RequestException(400, "Working intervals on the same day must not overlap.");
            }
        }
        var existing = await context.BusinessWorkingHours.Where(h => h.BusinessId == business.Id).ToListAsync(ct);
        context.BusinessWorkingHours.RemoveRange(existing);
        var replacement = request.Days.SelectMany(day => day.Intervals.Select(interval => new BusinessWorkingHours
        {
            BusinessId = business.Id, DayOfWeek = (DayOfWeek)day.DayOfWeek,
            StartTime = interval.StartTime!.Value, EndTime = interval.EndTime!.Value
        })).ToList();
        context.BusinessWorkingHours.AddRange(replacement);
        // A single SaveChanges transaction commits the complete replacement or none of it.
        await context.SaveChangesAsync(ct);
        return MapWeek(replacement);
    }

    public async Task<IReadOnlyList<BlockedPeriodResponse>> GetBlockedPeriodsAsync(Guid ownerId, CancellationToken ct)
    {
        var business = await businesses.GetOwnerAsync(ownerId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        return await context.BusinessBlockedPeriods.AsNoTracking()
            .Where(p => p.BusinessId == business.Id && p.EndUtc > now).OrderBy(p => p.StartUtc).ThenBy(p => p.Id)
            .Select(p => new BlockedPeriodResponse(p.Id, p.StartUtc, p.EndUtc, p.Reason)).ToListAsync(ct);
    }

    public async Task<BlockedPeriodResponse> CreateBlockedPeriodAsync(Guid ownerId, BlockedPeriodRequest request, CancellationToken ct)
    {
        var business = await businesses.GetOwnerAsync(ownerId, ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(business.TimeZoneId);
        var start = LocalToUtc(request.StartLocal, zone);
        var end = LocalToUtc(request.EndLocal, zone);
        if (start >= end || end - start > TimeSpan.FromDays(366))
            throw new RequestException(400, "Closure end must be after its start, with a maximum length of 366 days.");
        var period = new BusinessBlockedPeriod { BusinessId = business.Id, StartUtc = start, EndUtc = end,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim() };
        context.BusinessBlockedPeriods.Add(period);
        await context.SaveChangesAsync(ct);
        await context.Entry(period).ReloadAsync(ct);
        return new(period.Id, period.StartUtc, period.EndUtc, period.Reason);
    }

    public async Task DeleteBlockedPeriodAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        var business = await businesses.GetOwnerAsync(ownerId, ct);
        var period = await context.BusinessBlockedPeriods.SingleOrDefaultAsync(p => p.Id == id && p.BusinessId == business.Id, ct)
            ?? throw new RequestException(404, "Closure not found.");
        context.BusinessBlockedPeriods.Remove(period);
        await context.SaveChangesAsync(ct);
    }

    private static DateTime LocalToUtc(string value, TimeZoneInfo zone)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            || parsed.Year < 2000 || parsed.Year > 2100)
            throw new RequestException(400, "Use a business-local date/time in yyyy-MM-ddTHH:mm format, between years 2000 and 2100, without a UTC offset.");
        var local = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new RequestException(400, "Closure times must not fall in a skipped or repeated local hour during a daylight-saving transition. Choose an unambiguous time.");
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    private static WeeklyHoursResponse MapWeek(IEnumerable<BusinessWorkingHours> hours)
        => new(new[] { 1, 2, 3, 4, 5, 6, 0 }.Select(day => new WorkingDayResponse(day,
            hours.Where(h => (int)h.DayOfWeek == day).OrderBy(h => h.StartTime)
                .Select(h => new WorkingIntervalResponse(h.StartTime, h.EndTime)).ToList())).ToList());
}
