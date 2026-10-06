using Microsoft.AspNetCore.Identity;

namespace ServiceHub.Api.Models;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.NewGuid();
    }

    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Business? OwnedBusiness { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
