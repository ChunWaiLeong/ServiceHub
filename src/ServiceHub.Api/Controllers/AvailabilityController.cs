using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Contracts.Availability;
using ServiceHub.Api.Services;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Route("api/businesses/{businessId:guid}/availability")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AvailabilityController(AvailabilityService availability) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AvailabilityResponse>> Get(Guid businessId, [FromQuery] AvailabilityQuery query, CancellationToken ct)
        => Ok(await availability.GetAsync(businessId, query.ServiceId!.Value, query.Date!.Value, ct));
}
