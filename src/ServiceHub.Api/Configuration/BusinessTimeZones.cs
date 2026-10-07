using ServiceHub.Api.Contracts.Businesses;

namespace ServiceHub.Api.Configuration;

public static class BusinessTimeZones
{
    // Store IANA IDs now; scheduling and daylight-saving conversion are later work.
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
