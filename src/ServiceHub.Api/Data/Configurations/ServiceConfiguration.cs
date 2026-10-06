using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services", table =>
        {
            table.HasCheckConstraint("CK_Services_Name", "btrim(\"Name\") <> ''");
            table.HasCheckConstraint("CK_Services_Price", "\"Price\" > 0");
            table.HasCheckConstraint("CK_Services_Duration", "\"DurationMinutes\" > 0");
            table.HasCheckConstraint("CK_Services_Currency", "\"Currency\" ~ '^[A-Z]{3}$'");
        });
        builder.HasKey(service => service.Id);
        builder.HasAlternateKey(service => new { service.BusinessId, service.Id });
        builder.Property(service => service.Name).IsRequired().HasMaxLength(200);
        builder.Property(service => service.Description).IsRequired().HasMaxLength(2000);
        builder.Property(service => service.Price).HasPrecision(12, 2);
        builder.Property(service => service.Currency).IsRequired().HasMaxLength(3);
        builder.Property(service => service.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(service => service.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.HasOne(service => service.Business).WithMany(business => business.Services)
            .HasForeignKey(service => service.BusinessId).OnDelete(DeleteBehavior.Restrict);
    }
}
