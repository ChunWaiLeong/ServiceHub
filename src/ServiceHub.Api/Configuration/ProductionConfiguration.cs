using System.ComponentModel.DataAnnotations;

namespace ServiceHub.Api.Configuration;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Database")))
            errors.Add("ConnectionStrings:Database is required.");

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var jwtErrors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(jwt, new ValidationContext(jwt), jwtErrors, validateAllProperties: true))
            errors.Add("Jwt:Key, Jwt:Issuer and Jwt:Audience are required; Jwt:ExpirationMinutes must be 1–60.");
        if (!JwtOptions.HasStrongKey(jwt.Key))
            errors.Add("Jwt:Key must be a Base64-encoded random key of at least 32 bytes.");

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin => !IsHttpsOrigin(origin)))
            errors.Add("Cors:AllowedOrigins must contain exact HTTPS origins without paths, trailing slashes or wildcards.");

        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid Production configuration: " + string.Join(" ", errors));
    }

    private static bool IsHttpsOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(uri.Host)
        && !origin.Contains('*')
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
}
