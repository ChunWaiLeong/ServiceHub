using Microsoft.EntityFrameworkCore;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Data.Seeding;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var existing = await context.BusinessCategories.ToListAsync(cancellationToken);
        var categories = new (Guid Id, string Name)[]
        {
            (Guid.Parse("c1000000-0000-0000-0000-000000000001"), "Hair & Beauty"),
            (Guid.Parse("c1000000-0000-0000-0000-000000000002"), "Fitness"),
            (Guid.Parse("c1000000-0000-0000-0000-000000000003"), "Automotive"),
            (Guid.Parse("c1000000-0000-0000-0000-000000000004"), "Professional Services"),
            (Guid.Parse("c1000000-0000-0000-0000-000000000005"), "Home Services")
        };

        foreach (var (id, name) in categories)
        {
            if (existing.All(category => category.Id != id && category.Name != name))
            {
                context.BusinessCategories.Add(new BusinessCategory { Id = id, Name = name });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
