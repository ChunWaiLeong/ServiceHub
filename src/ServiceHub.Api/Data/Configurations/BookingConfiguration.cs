using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", table =>
        {
            table.HasCheckConstraint("CK_Bookings_Interval", "\"EndUtc\" > \"StartUtc\"");
            table.HasCheckConstraint("CK_Bookings_Status", "\"Status\" IN ('Confirmed', 'Cancelled', 'Completed')");
            table.HasCheckConstraint("CK_Bookings_ServiceName", "btrim(\"ServiceName\") <> ''");
            table.HasCheckConstraint("CK_Bookings_ServicePrice", "\"ServicePrice\" > 0");
            table.HasCheckConstraint("CK_Bookings_ServiceDuration", "\"ServiceDurationMinutes\" > 0");
            table.HasCheckConstraint("CK_Bookings_ServiceCurrency", "\"ServiceCurrency\" ~ '^[A-Z]{3}$'");
        });
        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(booking => booking.StartUtc).HasColumnType("timestamp with time zone");
        builder.Property(booking => booking.EndUtc).HasColumnType("timestamp with time zone");
        builder.Property(booking => booking.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(booking => booking.CancelledAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(booking => booking.ServiceName).IsRequired().HasMaxLength(200);
        builder.Property(booking => booking.ServicePrice).HasPrecision(12, 2);
        builder.Property(booking => booking.ServiceCurrency).IsRequired().HasMaxLength(3);
        builder.HasIndex(booking => new { booking.BusinessId, booking.StartUtc });
        builder.HasIndex(booking => new { booking.CustomerId, booking.StartUtc });
        builder.HasOne(booking => booking.Customer).WithMany(user => user.Bookings)
            .HasForeignKey(booking => booking.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(booking => booking.Business).WithMany(business => business.Bookings)
            .HasForeignKey(booking => booking.BusinessId).OnDelete(DeleteBehavior.Restrict);
        // Enforce service membership in the selected business at the database level.
        builder.HasOne(booking => booking.Service).WithMany(service => service.Bookings)
            .HasForeignKey(booking => new { booking.BusinessId, booking.ServiceId })
            .HasPrincipalKey(service => new { service.BusinessId, service.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
