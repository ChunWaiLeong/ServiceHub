using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Authentication;
using ServiceHub.Api.Contracts.Bookings;
using ServiceHub.Api.Services;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.BusinessOwner)]
[Route("api/owner/business/bookings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerBookingsController(BookingService bookings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> List([FromQuery] OwnerBookingQuery query, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await bookings.ListOwnerAsync(ownerId, query, ct));
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> Get(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await bookings.GetOwnerAsync(ownerId, id, ct));
    }
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<BookingResponse>> Cancel(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await bookings.CancelOwnerAsync(ownerId, id, ct));
    }
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<BookingResponse>> Complete(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var ownerId)) return Unauthorized();
        return Ok(await bookings.CompleteOwnerAsync(ownerId, id, ct));
    }
}
