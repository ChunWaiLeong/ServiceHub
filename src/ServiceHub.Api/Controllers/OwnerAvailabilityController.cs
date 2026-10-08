using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Authentication;
using ServiceHub.Api.Contracts.Availability;
using ServiceHub.Api.Services;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.BusinessOwner)]
[Route("api/owner/business")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerAvailabilityController(OwnerAvailabilityService availability) : ControllerBase
{
    [HttpGet("working-hours")]
    public async Task<ActionResult<WeeklyHoursResponse>> GetHours(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await availability.GetHoursAsync(ownerId, ct));
    }
    [HttpPut("working-hours")]
    public async Task<ActionResult<WeeklyHoursResponse>> SaveHours(WeeklyHoursRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await availability.ReplaceHoursAsync(ownerId, request, ct));
    }
    [HttpGet("blocked-periods")]
    public async Task<ActionResult<IReadOnlyList<BlockedPeriodResponse>>> ListClosures(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await availability.GetBlockedPeriodsAsync(ownerId, ct));
    }
    [HttpPost("blocked-periods")]
    public async Task<ActionResult<BlockedPeriodResponse>> CreateClosure(BlockedPeriodRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        var result = await availability.CreateBlockedPeriodAsync(ownerId, request, ct);
        return Created("/api/owner/business/blocked-periods", result);
    }
    [HttpDelete("blocked-periods/{id:guid}")]
    public async Task<IActionResult> DeleteClosure(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        await availability.DeleteBlockedPeriodAsync(ownerId, id, ct);
        return NoContent();
    }
}
