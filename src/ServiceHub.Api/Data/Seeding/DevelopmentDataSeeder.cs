namespace ServiceHub.Api.Data.Seeding;

public static class DevelopmentDataSeeder
{
    public static Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken) =>
        ReferenceDataSeeder.SeedAsync(context, cancellationToken);
}