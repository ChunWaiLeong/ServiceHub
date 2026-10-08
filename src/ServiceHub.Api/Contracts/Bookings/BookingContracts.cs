using System.ComponentModel.DataAnnotations;

namespace ServiceHub.Api.Contracts.Bookings;

public sealed record CreateBookingRequest([Required] Guid? ServiceId, [Required, StringLength(40)] string StartUtc);
public sealed record BookingCustomerResponse(string Name, string Email);
public sealed record BookingResponse(Guid Id, Guid BusinessId, string BusinessName, string BusinessAddress,
    string TimeZoneId, Guid ServiceId, string ServiceName, decimal ServicePrice, string ServiceCurrency,
    int ServiceDurationMinutes, DateTime StartUtc, DateTime EndUtc, string Status, DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc, BookingCustomerResponse? Customer);
public sealed class OwnerBookingQuery
{
    [RegularExpression("^(Confirmed|Cancelled|Completed)$")] public string? Status { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
}
