using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Authentication;
using ServiceHub.Api.Contracts.Services;
using ServiceHub.Api.Services;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Route("api/businesses/{businessId:guid}/services")]
public sealed class ServicesController(ServiceManagementService services) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceResponse>>> List(Guid businessId, CancellationToken ct)
        => Ok(await services.GetPublicServicesAsync(businessId, ct));

    [HttpGet("{serviceId:guid}")]
    public async Task<ActionResult<ServiceResponse>> Get(Guid businessId, Guid serviceId, CancellationToken ct)
        => Ok(await services.GetPublicAsync(businessId, serviceId, ct));

    [Authorize(Roles = ApplicationRoles.BusinessOwner)]
    [HttpPost]
    public async Task<ActionResult<ServiceResponse>> Create(Guid businessId, ServiceRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        var result = await services.CreateAsync(ownerId, businessId, request, ct);
        return CreatedAtAction(nameof(Get), new { businessId, serviceId = result.Id }, result);
    }

    [Authorize(Roles = ApplicationRoles.BusinessOwner)]
    [HttpPut("{serviceId:guid}")]
    public async Task<ActionResult<ServiceResponse>> Update(Guid businessId, Guid serviceId, ServiceRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await services.UpdateAsync(ownerId, businessId, serviceId, request, ct));
    }

    [Authorize(Roles = ApplicationRoles.BusinessOwner)]
    [HttpPatch("{serviceId:guid}/status")]
    public async Task<ActionResult<ServiceResponse>> Status(Guid businessId, Guid serviceId, ServiceStatusRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await services.SetStatusAsync(ownerId, businessId, serviceId, request.IsActive!.Value, ct));
    }
}
