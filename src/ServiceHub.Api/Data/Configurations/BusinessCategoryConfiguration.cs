using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data.Configurations;

public sealed class BusinessCategoryConfiguration : IEntityTypeConfiguration<BusinessCategory>
{
    public void Configure(EntityTypeBuilder<BusinessCategory> builder)
    {
        builder.ToTable("BusinessCategories", table =>
            table.HasCheckConstraint("CK_BusinessCategories_Name", "btrim(\"Name\") <> ''"));
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(category => category.Name).IsUnique();
    }
}
