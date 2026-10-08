using System.ComponentModel.DataAnnotations;

namespace ServiceHub.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    [Required] public string Key { get; init; } = "";
    [Required] public string Issuer { get; init; } = "";
    [Required] public string Audience { get; init; } = "";
    [Range(1, 60)] public int ExpirationMinutes { get; init; } = 30;

    public static bool HasStrongKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        try { return Convert.FromBase64String(key).Length >= 32; }
        catch (FormatException) { return false; }
    }
}
