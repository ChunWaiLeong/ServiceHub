using System.ComponentModel.DataAnnotations;

namespace ServiceHub.Api.Contracts.Availability;

public sealed record WorkingIntervalRequest([Required] TimeOnly? StartTime, [Required] TimeOnly? EndTime);
public sealed record WorkingDayRequest([Range(0, 6)] int DayOfWeek,
    [Required, MaxLength(8)] List<WorkingIntervalRequest> Intervals);
public sealed record WeeklyHoursRequest([Required, MaxLength(7)] List<WorkingDayRequest> Days);
public sealed record WorkingIntervalResponse(TimeOnly StartTime, TimeOnly EndTime);
public sealed record WorkingDayResponse(int DayOfWeek, IReadOnlyList<WorkingIntervalResponse> Intervals);
public sealed record WeeklyHoursResponse(IReadOnlyList<WorkingDayResponse> Days);
public sealed record BlockedPeriodRequest([Required, StringLength(16)] string StartLocal,
    [Required, StringLength(16)] string EndLocal, [StringLength(500)] string? Reason);
public sealed record BlockedPeriodResponse(Guid Id, DateTime StartUtc, DateTime EndUtc, string? Reason);
public sealed class AvailabilityQuery
{
    [Required] public Guid? ServiceId { get; init; }
    [Required] public DateOnly? Date { get; init; }
}
public sealed record AvailableSlotResponse(DateTime StartUtc, DateTime EndUtc);
public sealed record AvailabilityResponse(Guid BusinessId, Guid ServiceId, DateOnly Date,
    string TimeZoneId, int DurationMinutes, IReadOnlyList<AvailableSlotResponse> Slots);
