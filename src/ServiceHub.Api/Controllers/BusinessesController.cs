using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Authentication;
using ServiceHub.Api.Contracts.Businesses;
using ServiceHub.Api.Services;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Route("api/businesses")]
public sealed class BusinessesController(BusinessService businesses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BusinessPageResponse>> Browse([FromQuery] BusinessListQuery query, CancellationToken ct)
        => Ok(await businesses.BrowseAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await businesses.GetPublicAsync(id, ct));

    [Authorize(Roles = ApplicationRoles.BusinessOwner)]
    [HttpPost]
    public async Task<ActionResult<BusinessResponse>> Create(BusinessRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        var result = await businesses.CreateAsync(ownerId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [Authorize(Roles = ApplicationRoles.BusinessOwner)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BusinessResponse>> Update(Guid id, BusinessRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await businesses.UpdateAsync(ownerId, id, request, ct));
    }
}
