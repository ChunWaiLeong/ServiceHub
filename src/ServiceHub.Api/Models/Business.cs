namespace ServiceHub.Api.Models;

public sealed class Business
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid BusinessCategoryId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Address { get; set; }
    public string? ContactPhone { get; set; }
    public required string ContactEmail { get; set; }
    public required string TimeZoneId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ApplicationUser Owner { get; set; } = null!;
    public BusinessCategory BusinessCategory { get; set; } = null!;
    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<BusinessWorkingHours> WorkingHours { get; set; } = new List<BusinessWorkingHours>();
    public ICollection<BusinessBlockedPeriod> BlockedPeriods { get; set; } = new List<BusinessBlockedPeriod>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
