using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data.Configurations;

public sealed class BusinessBlockedPeriodConfiguration : IEntityTypeConfiguration<BusinessBlockedPeriod>
{
    public void Configure(EntityTypeBuilder<BusinessBlockedPeriod> builder)
    {
        builder.ToTable("BusinessBlockedPeriods", table =>
            table.HasCheckConstraint("CK_BlockedPeriods_Interval", "\"EndUtc\" > \"StartUtc\""));
        builder.HasKey(period => period.Id);
        builder.Property(period => period.StartUtc).HasColumnType("timestamp with time zone");
        builder.Property(period => period.EndUtc).HasColumnType("timestamp with time zone");
        builder.Property(period => period.Reason).HasMaxLength(500);
        builder.HasIndex(period => new { period.BusinessId, period.StartUtc });
        builder.HasOne(period => period.Business).WithMany(business => business.BlockedPeriods)
            .HasForeignKey(period => period.BusinessId).OnDelete(DeleteBehavior.Restrict);
    }
}
