using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(user => user.LastName).IsRequired().HasMaxLength(100);
        builder.Property(user => user.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.ToTable("AspNetUsers", table =>
        {
            table.HasCheckConstraint("CK_Users_FirstName", "btrim(\"FirstName\") <> ''");
            table.HasCheckConstraint("CK_Users_LastName", "btrim(\"LastName\") <> ''");
        });
    }
}
