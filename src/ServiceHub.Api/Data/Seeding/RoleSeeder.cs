using Microsoft.AspNetCore.Identity;
using ServiceHub.Api.Authentication;

namespace ServiceHub.Api.Data.Seeding;

public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<IdentityRole<Guid>> roles)
    {
        foreach (var name in ApplicationRoles.All)
        {
            if (await roles.RoleExistsAsync(name)) continue;
            var result = await roles.CreateAsync(new IdentityRole<Guid>(name));
            if (!result.Succeeded)
                throw new InvalidOperationException($"Unable to create the {name} role.");
        }
    }
}
