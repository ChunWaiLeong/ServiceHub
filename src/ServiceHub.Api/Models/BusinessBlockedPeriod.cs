namespace ServiceHub.Api.Models;

public sealed class BusinessBlockedPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Reason { get; set; }
    public Business Business { get; set; } = null!;
}
