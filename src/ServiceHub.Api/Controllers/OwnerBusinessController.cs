using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Authentication;
using ServiceHub.Api.Contracts.Businesses;
using ServiceHub.Api.Contracts.Services;
using ServiceHub.Api.Services;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.BusinessOwner)]
[Route("api/owner/business")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerBusinessController(BusinessService businesses, ServiceManagementService services) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BusinessResponse>> Get(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await businesses.GetOwnerAsync(ownerId, ct));
    }

    [HttpGet("services")]
    public async Task<ActionResult<IReadOnlyList<ServiceResponse>>> GetServices(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await services.GetOwnerServicesAsync(ownerId, ct));
    }
}
