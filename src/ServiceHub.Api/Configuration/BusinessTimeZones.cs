using ServiceHub.Api.Contracts.Businesses;

namespace ServiceHub.Api.Configuration;

public static class BusinessTimeZones
{
    // Store IANA IDs so availability uses the business zone on every server.
    public static readonly IReadOnlyList<TimeZoneResponse> Supported = Array.AsReadOnly(new[]
    {
        new TimeZoneResponse("Australia/Sydney", "Sydney / Melbourne / Canberra"),
        new TimeZoneResponse("Australia/Brisbane", "Brisbane"),
        new TimeZoneResponse("Australia/Adelaide", "Adelaide"),
        new TimeZoneResponse("Australia/Perth", "Perth"),
        new TimeZoneResponse("Australia/Darwin", "Darwin"),
        new TimeZoneResponse("Australia/Hobart", "Hobart")
    });
}
