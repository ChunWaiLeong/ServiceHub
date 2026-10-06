using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data.Configurations;

public sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("Businesses", table =>
            table.HasCheckConstraint("CK_Businesses_Name", "btrim(\"Name\") <> ''"));
        builder.HasKey(business => business.Id);
        builder.Property(business => business.Name).IsRequired().HasMaxLength(200);
        builder.Property(business => business.Description).IsRequired().HasMaxLength(2000);
        builder.Property(business => business.Address).IsRequired().HasMaxLength(500);
        builder.Property(business => business.ContactPhone).HasMaxLength(30);
        builder.Property(business => business.ContactEmail).IsRequired().HasMaxLength(254);
        builder.Property(business => business.TimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(business => business.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(business => business.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(business => business.OwnerId).IsUnique();
        builder.HasOne(business => business.Owner).WithOne(user => user.OwnedBusiness)
            .HasForeignKey<Business>(business => business.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(business => business.BusinessCategory).WithMany(category => category.Businesses)
            .HasForeignKey(business => business.BusinessCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
