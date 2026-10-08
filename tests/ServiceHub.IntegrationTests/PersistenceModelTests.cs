using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceHub.Api.Data;
using ServiceHub.Api.Models;
using Xunit;

namespace ServiceHub.IntegrationTests;

public sealed class PersistenceModelTests
{
    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=localhost;Database=servicehub").Options);

    [Fact]
    public void OwnerAndCategoryRelationships_AreExplicit()
    {
        using var context = CreateContext();
        var business = context.Model.FindEntityType(typeof(Business))!;
        var owner = Assert.Single(business.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        Assert.True(owner.IsUnique);
        Assert.Equal(nameof(Business.OwnerId), Assert.Single(owner.Properties).Name);
        Assert.Contains(business.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Business.OwnerId));
        Assert.Contains(business.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(BusinessCategory));
        var category = context.Model.FindEntityType(typeof(BusinessCategory))!;
        Assert.Contains(category.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(BusinessCategory.Name));
    }

    [Fact]
    public void BookingServiceRelationship_EnforcesBusinessMembership()
    {
        using var context = CreateContext();
        var booking = context.Model.FindEntityType(typeof(Booking))!;
        var serviceKey = Assert.Single(booking.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Service));
        Assert.Equal(new[] { "BusinessId", "ServiceId" }, serviceKey.Properties.Select(property => property.Name));
        Assert.Equal(new[] { "BusinessId", "Id" }, serviceKey.PrincipalKey.Properties.Select(property => property.Name));
    }

    [Fact]
    public void DomainForeignKeys_RestrictDeletion()
    {
        using var context = CreateContext();
        var domainTypes = new[] { typeof(Business), typeof(Service), typeof(Booking), typeof(BusinessWorkingHours), typeof(BusinessBlockedPeriod) };
        var foreignKeys = domainTypes.SelectMany(type => context.Model.FindEntityType(type)!.GetForeignKeys()).ToList();
        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
    }

    [Fact]
    public void MoneySnapshotsAndTimes_UsePostgresTypes()
    {
        using var context = CreateContext();
        var service = context.Model.FindEntityType(typeof(Service))!;
        var price = service.FindProperty(nameof(Service.Price))!;
        Assert.Equal(12, price.GetPrecision());
        Assert.Equal(2, price.GetScale());
        var booking = context.Model.FindEntityType(typeof(Booking))!;
        Assert.Equal(12, booking.FindProperty(nameof(Booking.ServicePrice))!.GetPrecision());
        Assert.Equal(2, booking.FindProperty(nameof(Booking.ServicePrice))!.GetScale());
        Assert.Equal("timestamp with time zone", booking.FindProperty(nameof(Booking.StartUtc))!.GetColumnType());
        Assert.Equal("time without time zone", context.Model.FindEntityType(typeof(BusinessWorkingHours))!
            .FindProperty(nameof(BusinessWorkingHours.StartTime))!.GetColumnType());
        Assert.Null(context.Model.FindEntityType(typeof(BusinessWorkingHours))!.GetIndexes()
            .FirstOrDefault(index => index.IsUnique && index.Properties.Any(property => property.Name == "DayOfWeek")));
    }

    [Fact]
    public void DatabaseChecks_ValidatePricesDurationsAndIntervals()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        Assert.Contains(model.FindEntityType(typeof(Service))!.GetCheckConstraints(), check => check.Name == "CK_Services_Price");
        Assert.Contains(model.FindEntityType(typeof(Service))!.GetCheckConstraints(), check => check.Name == "CK_Services_Duration");
        Assert.Contains(model.FindEntityType(typeof(BusinessWorkingHours))!.GetCheckConstraints(), check => check.Name == "CK_WorkingHours_Interval");
        Assert.Contains(model.FindEntityType(typeof(BusinessBlockedPeriod))!.GetCheckConstraints(), check => check.Name == "CK_BlockedPeriods_Interval");
        Assert.Contains(model.FindEntityType(typeof(Booking))!.GetCheckConstraints(), check => check.Name == "CK_Bookings_Interval");
    }

    [Fact]
    public void Migration_GeneratesServiceHubSchemaWithoutLegacyOrExclusionConstraints()
    {
        using var context = CreateContext();
        var migrations = context.Database.GetMigrations().ToList();
        Assert.Contains("20261008083922_ProtectConfirmedBookingIntervals", migrations);
        Assert.EndsWith("_InitialServiceHub", migrations[0]);
        var script = context.GetService<IMigrator>().GenerateScript(toMigration: migrations[0]);
        Assert.Contains("CREATE TABLE \"Businesses\"", script);
        Assert.Contains("numeric(12,2)", script);
        Assert.Contains("ON DELETE RESTRICT", script);
        Assert.DoesNotContain("GoVilla", script);
        Assert.DoesNotContain("EXCLUDE", script);
        Assert.False(context.Database.HasPendingModelChanges());
    }
    [Fact]
    public void BookingMigration_AddsBusinessScopedConfirmedOnlyExclusion()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript(fromMigration: "20261006142457_InitialServiceHub");
        Assert.Contains("CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public", script);
        Assert.Contains("EXCLUDE USING gist", script);
        Assert.Contains("public.gist_uuid_ops WITH =", script);
        Assert.Contains("tstzrange(\"StartUtc\", \"EndUtc\", '[)') WITH &&", script);
        Assert.Contains("WHERE (\"Status\" = 'Confirmed')", script);
        Assert.DoesNotContain("GoVilla", script);
        Assert.False(context.Database.HasPendingModelChanges());
    }

}
