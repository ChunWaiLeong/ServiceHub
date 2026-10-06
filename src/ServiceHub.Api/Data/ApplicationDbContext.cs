using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // Users is inherited from IdentityDbContext.
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessCategory> BusinessCategories => Set<BusinessCategory>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<BusinessWorkingHours> BusinessWorkingHours => Set<BusinessWorkingHours>();
    public DbSet<BusinessBlockedPeriod> BusinessBlockedPeriods => Set<BusinessBlockedPeriod>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
