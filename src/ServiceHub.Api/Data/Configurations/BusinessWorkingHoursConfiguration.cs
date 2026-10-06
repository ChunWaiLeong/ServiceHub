using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data.Configurations;

public sealed class BusinessWorkingHoursConfiguration : IEntityTypeConfiguration<BusinessWorkingHours>
{
    public void Configure(EntityTypeBuilder<BusinessWorkingHours> builder)
    {
        builder.ToTable("BusinessWorkingHours", table =>
        {
            table.HasCheckConstraint("CK_WorkingHours_DayOfWeek", "\"DayOfWeek\" BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_WorkingHours_Interval", "\"EndTime\" > \"StartTime\"");
        });
        builder.HasKey(hours => hours.Id);
        builder.Property(hours => hours.DayOfWeek).HasConversion<int>();
        builder.Property(hours => hours.StartTime).HasColumnType("time without time zone");
        builder.Property(hours => hours.EndTime).HasColumnType("time without time zone");
        builder.HasIndex(hours => new { hours.BusinessId, hours.DayOfWeek });
        builder.HasOne(hours => hours.Business).WithMany(business => business.WorkingHours)
            .HasForeignKey(hours => hours.BusinessId).OnDelete(DeleteBehavior.Restrict);
    }
}
