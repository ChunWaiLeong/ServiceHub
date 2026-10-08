using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Authentication;
using ServiceHub.Api.Contracts.Bookings;
using ServiceHub.Api.Services;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.Customer + "," + ApplicationRoles.BusinessOwner)]
[Route("api/bookings")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BookingsController(BookingService bookings) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Customer)]
    public async Task<ActionResult<BookingResponse>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var result = await bookings.CreateAsync(userId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpGet("/api/me/bookings")]
    [Authorize(Roles = ApplicationRoles.Customer)]
    public async Task<ActionResult<IReadOnlyList<BookingResponse>>> Mine(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        return Ok(await bookings.ListCustomerAsync(userId, ct));
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> Get(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        return Ok(User.IsInRole(ApplicationRoles.BusinessOwner)
            ? await bookings.GetOwnerAsync(userId, id, ct) : await bookings.GetCustomerAsync(userId, id, ct));
    }
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = ApplicationRoles.Customer)]
    public async Task<ActionResult<BookingResponse>> Cancel(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        return Ok(await bookings.CancelCustomerAsync(userId, id, ct));
    }
}
