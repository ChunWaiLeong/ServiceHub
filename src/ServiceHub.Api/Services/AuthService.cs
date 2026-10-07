using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceHub.Api.Authentication;
using ServiceHub.Api.Contracts.Auth;
using ServiceHub.Api.Data;
using ServiceHub.Api.Models;

namespace ServiceHub.Api.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext context,
    JwtTokenService tokens)
{
    public async Task<string[]> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request.Role is not (ApplicationRoles.Customer or ApplicationRoles.BusinessOwner))
            return ["Choose Customer or BusinessOwner as the account type."];

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
            Email = email, UserName = email, LockoutEnabled = true
        };
        // User creation and role assignment must succeed together.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                // Password policy feedback is useful; duplicate/other account errors stay generic.
                return result.Errors.Any(error => error.Code.StartsWith("Password", StringComparison.Ordinal))
                    ? ["Use at least 12 characters including uppercase, lowercase, a number, and a symbol."]
                    : ["Unable to register with those details."];
            }
            var roleResult = await users.AddToRoleAsync(user, request.Role);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Unable to assign the registration role.");
            await transaction.CommitAsync(cancellationToken);
            return [];
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UserNameIndex" })
        {
            // The unique username index also protects concurrent registrations for the same email.
            return ["Unable to register with those details."];
        }
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive) return null;
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return null;
        var profile = await GetUserAsync(user);
        return profile is null ? null : tokens.CreateToken(profile);
    }

    public async Task<AuthUserResponse?> GetCurrentUserAsync(Guid id)
    {
        var user = await users.FindByIdAsync(id.ToString());
        return user is null || !user.IsActive ? null : await GetUserAsync(user);
    }

    private async Task<AuthUserResponse?> GetUserAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var role = roles.SingleOrDefault();
        if (role is null || !ApplicationRoles.All.Contains(role)) return null;
        return new AuthUserResponse(user.Id, user.FirstName, user.LastName, user.Email!, role);
    }
}
