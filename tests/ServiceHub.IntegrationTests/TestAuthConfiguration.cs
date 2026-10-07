using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ServiceHub.IntegrationTests;

internal static class TestAuthConfiguration
{
    // Ephemeral test signing material, never a development or production credential.
    public static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public const string Issuer = "ServiceHub.Tests";
    public const string Audience = "ServiceHub.Tests.Client";

    public static void Configure(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, configuration) =>
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Key, ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience,
            ["Jwt:ExpirationMinutes"] = "30"
        }));
}
