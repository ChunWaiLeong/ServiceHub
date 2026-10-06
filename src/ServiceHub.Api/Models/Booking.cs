namespace ServiceHub.Api.Models;

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid BusinessId { get; set; }
    public Guid ServiceId { get; set; }
    // Appointment intervals are half-open: [StartUtc, EndUtc).
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAtUtc { get; set; }
    public required string ServiceName { get; set; }
    public decimal ServicePrice { get; set; }
    public required string ServiceCurrency { get; set; }
    public int ServiceDurationMinutes { get; set; }
    public ApplicationUser Customer { get; set; } = null!;
    public Business Business { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
